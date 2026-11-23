using System.Net;
using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects;

namespace Cloyd.Hub;

sealed partial class TheHub
{
   static readonly JsonSerializerOptions WorktreeJson = new(JsonSerializerDefaults.Web) { RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true };

   bool Advertises(string capability)
   {
      var announcement = new FileWrittenWhole(Path.Combine(_folderOfTheAnnouncement.FullName, WhatTheAnnouncementSays.FileName));
      using var fields = JsonDocument.Parse(announcement.Text() ?? "{}");
      return fields.RootElement.TryGetProperty(capability, out var supported) && supported.ValueKind == JsonValueKind.True;
   }

   public async Task<WorktreeSessionsPreview> PreviewWorktreeSessions(Project worktree, CancellationToken cancellation)
   {
      if(!Advertises("supportsWorktreeSessionRemoval"))
         throw new InvalidOperationException("The running hub does not yet offer worktree session removal.");
      using var response = await Send(hub => new HttpRequestMessage(HttpMethod.Post, hub.AddressOf("worktree-sessions/preview"))
      {
         Content = JsonContent.Create(new { worktreePath = worktree.Folder })
      }, cancellation);
      if(response.StatusCode == HttpStatusCode.NotFound)
         throw new InvalidOperationException("The running hub does not yet offer worktree session removal.");
      return await Read<WorktreeSessionsPreview>(response, cancellation, WorktreeJson);
   }

   public async Task<WorktreeSessionsResult> ApplyWorktreeDisposition(WorktreeSessionsPreview reviewed, string? disposition, CancellationToken cancellation)
   {
      var request = new
      {
         reviewed.OperationId, disposition,
         sessions = reviewed.Sessions.Select(it => new { it.AgentCli, it.SessionId, hostId = (string?)null }),
         shellOnlyHostIds = Array.Empty<string>()
      };
      using var response = await Send(hub => new HttpRequestMessage(HttpMethod.Post, hub.AddressOf("worktree-sessions/apply"))
      {
         Content = JsonContent.Create(request)
      }, cancellation);
      var result = await Read<WorktreeSessionsResult>(response, cancellation, WorktreeJson);
      if(response.StatusCode != HttpStatusCode.Conflict || result.Status == "completed") response.EnsureSuccessStatusCode();
      return result;
   }
}
