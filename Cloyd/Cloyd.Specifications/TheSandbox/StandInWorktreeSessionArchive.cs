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
   public void ResetApplied() => Applied = null;
   public bool LoseNextReceipt { get; set; }

   public JsonObject? OccupantRequest { get; private set; }
   public Func<WorktreeSessionsPreview>? PreviewNow { get; init; }
   public Func<ReviewedOccupants>? OccupantsNow { get; init; }
   public Func<OccupantTarget[], OccupantActionResults>? OccupantAction { get; init; }

   public StandInWorktreeSessionArchive(DirectoryInfo folder, WorktreeSessionsPreview preview, ReviewedOccupants? occupants = null)
   {
      var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
      builder.WebHost.UseKestrelCore().ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
      builder.Services.AddRoutingCore();
      _api = builder.Build();
      _api.MapGet("/session-hosts", () => Results.Json(new { sessionHosts = Array.Empty<object>() }));
      _api.MapPost("/worktree-sessions/occupants/review", () => Results.Json(OccupantsNow?.Invoke() ?? occupants));
      _api.MapPost("/worktree-sessions/occupants/act", async (HttpContext exchange) =>
      {
         OccupantRequest = await exchange.Request.ReadFromJsonAsync<JsonObject>();
         var targets = OccupantRequest!["targets"]!.Deserialize<OccupantTarget[]>(JsonSerializerOptions.Web)!;
         return Results.Json(OccupantAction?.Invoke(targets) ?? new OccupantActionResults(targets.Select(it => new OccupantActionResult(it, "skipped", "Activity changed after review.")).ToArray()));
      });
      _api.MapPost("/worktree-sessions/preview", async exchange =>
      {
         var request = await exchange.Request.ReadFromJsonAsync<JsonObject>();
         if(request!["worktreePath"]!.GetValue<string>() != preview.WorktreePath) { exchange.Response.StatusCode = 400; return; }
         var listed = Applied is not null && Result?.Status == "completed" ? preview with { Sessions = [], ShellOnlyHosts = [] } : PreviewNow?.Invoke() ?? preview;
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
      announcement["supportsWorktreeOccupantActions"] = occupants is not null;
      File.WriteAllText(Path.Combine(folder.FullName, WhatTheAnnouncementSays.FileName), announcement.ToJsonString());
      Hub = TheHub.AnnouncedIn(folder);
   }

   public async ValueTask DisposeAsync()
   {
      await _api.StopAsync();
      await _api.DisposeAsync();
   }
}
