using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects;

namespace Cloyd.Hub;

sealed partial class TheHub
{
   public async Task<ReviewedOccupants?> ReviewWorktreeOccupants(Project worktree, CancellationToken cancellation)
   {
      if(!Advertises("supportsWorktreeOccupantActions")) return null;
      return await OccupantRequest<ReviewedOccupants>("review", new { worktreePath = worktree.Folder }, cancellation);
   }

   public async Task<OccupantActionResults> ActOnWorktreeOccupants(Project worktree, string action, OccupantTarget[] targets, CancellationToken cancellation)
   {
      var result = await OccupantRequest<OccupantActionResults>("act", new { worktreePath = worktree.Folder, action, targets }, cancellation);
      if(result.Results.Length != targets.Length || !targets.ToHashSet().SetEquals(result.Results.Select(it => it.Target)))
         throw new InvalidDataException("The hub returned results for different occupants. Refresh to see their current state.");
      return result;
   }

   async Task<T> OccupantRequest<T>(string route, object body, CancellationToken cancellation)
   {
      using var response = await Send(hub => new HttpRequestMessage(HttpMethod.Post, hub.AddressOf("worktree-sessions/occupants/" + route))
         { Content = JsonContent.Create(body) }, cancellation);
      response.EnsureSuccessStatusCode();
      return await Read<T>(response, cancellation, WorktreeJson);
   }
}
