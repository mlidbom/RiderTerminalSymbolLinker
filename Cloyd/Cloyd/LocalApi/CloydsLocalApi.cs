using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Threading;
using Cloyd.ClaudeCode;
using Cloyd.LocalApi.Contract;
using Cloyd.Projects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cloyd.LocalApi;

/// <summary>
/// The Cloyd application's local API while it listens: ASP.NET Core's web server, Kestrel, on a free port of the loopback
/// address only, announced in its <see cref="AnnouncementOfCloydsLocalApi"/> once it listens. Its requests:
/// <list type="bullet">
/// <item><c>GET /windows</c>: the project windows the application shows, as a <see cref="WindowsAnswer"/>.</item>
/// <item>
/// <c>POST /windows/show</c> with a <see cref="WindowToShowInTheRequest"/>: shows the project's window, bringing it forward
/// when it is open, else opening it, and answers with it, a <see cref="WindowInTheAnswer"/>, once it shows.
/// <c>Cloyd.exe --project</c> asks it.
/// </item>
/// <item>
/// <c>POST /sessions/show?session=&lt;session id&gt;&amp;process=&lt;Claude Code's process id&gt;</c>: shows the tab whose Claude
/// Code runs as the process, else the one that runs the session or ran it last, selecting it and bringing its window
/// forward, and answers with the window, a <see cref="WindowInTheAnswer"/>: 404 when no window has such a tab. Either may
/// be left out, not both. A click on a notification of the session asks it, through the hub, which may let Cloyd take the
/// foreground first (<see cref="TheForeground"/>).
/// </item>
/// </list>
/// </summary>
/// <remarks>
/// Any process on the machine can reach a loopback port, so every request must carry the announcement's token, else it
/// is answered 401, and name the API's own address as its <c>Host</c>, else 403: a web page can't then reach it through
/// a name of its own that resolves to the loopback address. As the plugin's and the hub's APIs are guarded.
/// </remarks>
sealed class CloydsLocalApi
{
   /// <summary>How long stopping waits for the requests under way, which take milliseconds.</summary>
   static readonly TimeSpan TimeToStop = TimeSpan.FromSeconds(2);

   readonly WebApplication _web;
   readonly CloydsFolder _folder;

   CloydsLocalApi(WebApplication web, CloydsFolder folder)
   {
      _web = web;
      _folder = folder;
   }

   /// <summary>
   /// Starts the API for <paramref name="windows"/>, and announces it in <paramref name="folder"/> once it listens. Its
   /// requests are answered on the thread pool, and what they ask of the windows is done on the UI thread.
   /// </summary>
   public static CloydsLocalApi Start(ProjectWindows windows, CloydsFolder folder)
   {
      var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
      var web = TheWebApplication();
      web.Use(AnswerWithItsFailure);
      web.Use((exchange, next) => Guard(exchange, next, token));
      web.MapGet("/windows", exchange => AnswerWithTheWindows(exchange, windows));
      web.MapPost("/windows/show", exchange => Show(exchange, windows));
      web.MapPost("/sessions/show", exchange => ShowTheTabOfASession(exchange, windows));
      Task.Run(() => web.StartAsync()).GetAwaiter().GetResult();
      new AnnouncementOfCloydsLocalApi(CloydsLocalApiProtocol.Version, Environment.ProcessId, new Uri(web.Urls.Single()).Port, token).WriteIn(folder);
      return new(web, folder);
   }

   /// <summary>Withdraws the announcement, so that no client finds the API stopping, then stops listening.</summary>
   public void Stop()
   {
      AnnouncementOfCloydsLocalApi.WithdrawFrom(_folder);
      Task.Run(async () =>
      {
         await _web.StopAsync();
         await _web.DisposeAsync();
      }).GetAwaiter().GetResult();
   }

   static WebApplication TheWebApplication()
   {
      var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
      builder.WebHost.UseKestrelCore().ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
      builder.Services.AddRoutingCore();
      builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeToStop);
      return builder.Build();
   }

   static async Task AnswerWithTheWindows(HttpContext exchange, ProjectWindows windows)
   {
      var open = await Dispatcher.UIThread.InvokeAsync(() => windows.Open.Select(InTheAnswer).ToList());
      await Reply(exchange, StatusCodes.Status200OK, new WindowsAnswer(open));
   }

   static async Task Show(HttpContext exchange, ProjectWindows windows)
   {
      var request = await JsonSerializer.DeserializeAsync<WindowToShowInTheRequest>(exchange.Request.Body, CloydsLocalApiProtocol.Json, exchange.RequestAborted);
      if(request?.Project is not {Length: > 0} folder || !Directory.Exists(folder))
      {
         await Reply(exchange, StatusCodes.Status400BadRequest, new ErrorAnswer($"The request must name the folder of a project that exists, as {{\"project\": <its full path>}}: it named {request?.Project ?? "none"}."));
         return;
      }

      var shown = await Dispatcher.UIThread.InvokeAsync(() => InTheAnswer(windows.Show(new Project(folder))));
      await Reply(exchange, StatusCodes.Status200OK, shown);
   }

   static async Task ShowTheTabOfASession(HttpContext exchange, ProjectWindows windows)
   {
      var query = exchange.Request.Query;
      var session = query["session"] is [{Length: > 0} sessionId] ? new ClaudeCodeSessionId(sessionId) : (ClaudeCodeSessionId?)null;
      var claudeCode = query["process"] is [{} processId] && int.TryParse(processId, out var process) ? new ClaudeCodeProcessId(process) : (ClaudeCodeProcessId?)null;
      if(session is null && claudeCode is null)
      {
         await Reply(exchange, StatusCodes.Status400BadRequest, new ErrorAnswer($"The request must name the session, as ?session=<its id>, or its Claude Code's process, as ?process=<its id as a number>, or both: it asked {exchange.Request.QueryString}."));
         return;
      }

      var shown = await Dispatcher.UIThread.InvokeAsync(() => windows.ShowTheTabOf(session, claudeCode) is {} window ? InTheAnswer(window) : null);
      if(shown is not null)
         await Reply(exchange, StatusCodes.Status200OK, shown);
      else
         await Reply(exchange, StatusCodes.Status404NotFound, new ErrorAnswer($"No tab of a project window runs the Claude Code of process {claudeCode?.ToString() ?? "none named"}, nor runs or ran last the session {session?.ToString() ?? "none named"}."));
   }

   static WindowInTheAnswer InTheAnswer(ProjectWindow window) => new(window.Project.Folder, window.Title ?? "");

   /// <summary>Lets the request of <paramref name="exchange"/> on to <paramref name="next"/>, or answers it 403 or 401: see the API's remarks.</summary>
   static async Task Guard(HttpContext exchange, RequestDelegate next, string token)
   {
      if(!NamesTheLoopbackPort(exchange))
         await Reply(exchange, StatusCodes.Status403Forbidden, new ErrorAnswer($"The Host header must be 127.0.0.1:{exchange.Connection.LocalPort} or localhost:{exchange.Connection.LocalPort}."));
      else if(exchange.Request.Headers.Authorization != $"Bearer {token}")
         await Reply(exchange, StatusCodes.Status401Unauthorized, new ErrorAnswer("The request must carry the token of the announcement, as \"Authorization: Bearer <token>\"."));
      else
         await next(exchange);
   }

   static bool NamesTheLoopbackPort(HttpContext exchange) =>
      exchange.Request.Host is { HasValue: true } host
   && host.Port == exchange.Connection.LocalPort
   && (host.Host == "127.0.0.1" || host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));

   /// <summary>Each request's boundary: a failure answering it is answered 500, unless the answer has started, and the API serves on.</summary>
   static async Task AnswerWithItsFailure(HttpContext exchange, RequestDelegate next)
   {
      try
      {
         await next(exchange);
      }
      catch(Exception) when(exchange.RequestAborted.IsCancellationRequested)
      {
         // The client went: there is nobody to answer.
      }
      catch(Exception bug) // A request handler's boundary, which answers with the failure.
      {
         if(exchange.Response.HasStarted) return;
         await Reply(exchange, StatusCodes.Status500InternalServerError, new ErrorAnswer($"{bug.GetType().Name}: {bug.Message}"));
      }
   }

   static async Task Reply<TAnswer>(HttpContext exchange, int status, TAnswer answer)
   {
      exchange.Response.StatusCode = status;
      exchange.Response.ContentType = "application/json; charset=utf-8";
      await exchange.Response.WriteAsync(JsonSerializer.Serialize(answer, CloydsLocalApiProtocol.Json));
   }
}
