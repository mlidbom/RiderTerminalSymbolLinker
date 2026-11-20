using System.Net;
using System.Text.Json;
using Cloyd.Ides;
using Cloyd.Projects;
using Cloyd.VirtualDesktops;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// A JetBrains IDE that runs Claude Sessions, as Cloyd sees one: its local API, announced in the
/// <see cref="Sandbox.FolderOfTheIdes"/> of a sandbox, whose <c>GET /windows</c> lists its project windows on the virtual desktops a
/// specification <see cref="Shows"/> them on. The announcement names the specifications' own process, which runs. It
/// <see cref="Exits"/> when the specification is done with it, unless it has.
/// </summary>
sealed class StandInIde : IDisposable
{
   readonly WebApplication _api;
   readonly string _announcement;

   /// <summary>The windows it lists, swapped whole as a specification changes them, and read by its API's threads.</summary>
   volatile IReadOnlyList<WindowAnIdeListed> _windows = [];

   bool _exited;

   StandInIde(Sandbox sandbox, string name)
   {
      var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
      builder.WebHost.UseKestrelCore().ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
      builder.Services.AddRoutingCore();
      _api = builder.Build();
      _api.MapGet("/windows", exchange => exchange.Response.WriteAsJsonAsync(new WindowsAnIdeListed(_windows), JsonSerializerOptions.Web));
      Task.Run(() => _api.StartAsync()).GetAwaiter().GetResult();

      _announcement = Path.Combine(Directory.CreateDirectory(sandbox.FolderOfTheIdes).FullName, $"{name}.json");
      File.WriteAllText(_announcement, JsonSerializer.Serialize(new AnnouncementOfAnIdesLocalApi(Environment.ProcessId, new Uri(_api.Urls.Single()).Port), JsonSerializerOptions.Web));
   }

   /// <summary>An IDE announced in <paramref name="sandbox"/> as <paramref name="name"/>, with no project open.</summary>
   public static StandInIde In(Sandbox sandbox, string name) => new(sandbox, name);

   /// <summary>Has the IDE show <paramref name="project"/> in its one window, on <paramref name="desktop"/>.</summary>
   public void Shows(Project project, VirtualDesktop desktop) => _windows = [new WindowAnIdeListed(project.Folder, desktop.Id)];

   /// <summary>Exits, as an IDE does: withdraws its announcement, and stops its API.</summary>
   public void Exits()
   {
      File.Delete(_announcement);
      Task.Run(async () =>
      {
         await _api.StopAsync();
         await _api.DisposeAsync();
      }).GetAwaiter().GetResult();
      _exited = true;
   }

   public void Dispose()
   {
      if(!_exited) Exits();
   }
}
