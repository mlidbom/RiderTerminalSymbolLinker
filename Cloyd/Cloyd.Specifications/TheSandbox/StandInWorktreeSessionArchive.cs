using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeTooling.Hub.LocalApi.Contract;
using Cloyd.Hub;
using Cloyd.Hub.Worktrees;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>Serves the archive preview and receipt for a dashboard specification. The
/// specification controls activity, refusals and partial receipts without closing a real host.</summary>
sealed class StandInWorktreeSessionArchive : IAsyncDisposable
{
   readonly WebApplication _api;
   public TheHub Hub { get; }
   public JsonObject? Applied { get; private set; }
   public WorktreeSessionsResult? Result { get; set; }
   public bool LoseNextReceipt { get; set; }

   public StandInWorktreeSessionArchive(DirectoryInfo folder, WorktreeSessionsPreview preview)
   {
      var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
      builder.WebHost.UseKestrelCore().ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
      builder.Services.AddRoutingCore();
      _api = builder.Build();
      _api.MapPost("/worktree-sessions/preview", async exchange =>
      {
         var request = await exchange.Request.ReadFromJsonAsync<JsonObject>();
         if(request!["worktreePath"]!.GetValue<string>() != preview.WorktreePath) { exchange.Response.StatusCode = 400; return; }
         var listed = Applied is not null && Result?.Status == "completed" ? preview with { Sessions = [], ShellOnlyHosts = [] } : preview;
         await exchange.Response.WriteAsJsonAsync(listed, JsonSerializerOptions.Web);
      });
      _api.MapPost("/worktree-sessions/apply", async exchange =>
      {
         Applied = await exchange.Request.ReadFromJsonAsync<JsonObject>();
         if(LoseNextReceipt)
         {
            LoseNextReceipt = false;
            exchange.Response.StatusCode = 503;
            return;
         }
         exchange.Response.StatusCode = Result?.Status == "completed" ? 200 : 409;
         await exchange.Response.WriteAsJsonAsync(Result, JsonSerializerOptions.Web);
      });
      Task.Run(() => _api.StartAsync()).GetAwaiter().GetResult();
      folder.Create();
      var announcement = JsonSerializer.SerializeToNode(new WhatTheAnnouncementSays(LocalApiProtocol.Version, new Uri(_api.Urls.Single()).Port, "sandbox", Environment.ProcessId,
                                                                                    DateTime.UtcNow, folder.FullName, null))!.AsObject();
      announcement["supportsWorktreeSessionRemoval"] = true;
      File.WriteAllText(Path.Combine(folder.FullName, WhatTheAnnouncementSays.FileName), announcement.ToJsonString());
      Hub = TheHub.AnnouncedIn(folder);
   }

   public async ValueTask DisposeAsync()
   {
      await _api.StopAsync();
      await _api.DisposeAsync();
   }
}
