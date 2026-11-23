using Cloyd.Hub.Worktrees;
using Cloyd.Projects;

namespace Cloyd.Hub;

sealed partial class TheHub
{
   const string DirectoryCleanupRoute = "worktree-directory-cleanup";
   public bool SupportsWorktreeDirectoryCleanup => Advertises("supportsWorktreeDirectoryCleanup");

   public async Task<WorktreeDirectoryCleanup?> PrepareDirectoryCleanup(Project worktree, CancellationToken cancellation)
   {
      if(!SupportsWorktreeDirectoryCleanup) return null;
      using var response = await Send(hub => new HttpRequestMessage(HttpMethod.Post, hub.AddressOf(DirectoryCleanupRoute + "/prepare"))
         { Content = JsonContent.Create(new { worktreePath = worktree.Folder }) }, cancellation);
      response.EnsureSuccessStatusCode();
      var receipt = await Read<WorktreeDirectoryCleanup>(response, cancellation, WorktreeJson);
      receipt.Require(worktree, null, "prepared");
      return receipt;
   }

   public async Task<WorktreeDirectoryCleanup> EnqueueDirectoryCleanup(Project worktree, Guid id, CancellationToken cancellation)
   {
      using var response = await Send(hub => new HttpRequestMessage(HttpMethod.Post, hub.AddressOf($"{DirectoryCleanupRoute}/{id:D}/enqueue")), cancellation);
      response.EnsureSuccessStatusCode();
      var receipt = await Read<WorktreeDirectoryCleanup>(response, cancellation, WorktreeJson);
      receipt.Require(worktree, id, "pending", "completed", "needsReview");
      return receipt;
   }

   public async Task<WorktreeDirectoryCleanup[]> OutstandingDirectoryCleanups(CancellationToken cancellation)
   {
      if(!SupportsWorktreeDirectoryCleanup) return [];
      using var response = await Send(hub => new HttpRequestMessage(HttpMethod.Get, hub.AddressOf(DirectoryCleanupRoute)), cancellation);
      response.EnsureSuccessStatusCode();
      var list = await Read<DirectoryCleanupJobs>(response, cancellation, WorktreeJson);
      return list.Jobs;
   }

   public async Task<WorktreeDirectoryCleanup?> OutstandingDirectoryCleanup(Project worktree, CancellationToken cancellation) =>
      (await OutstandingDirectoryCleanups(cancellation)).FirstOrDefault(it => string.Equals(it.WorktreePath, worktree.Folder, StringComparison.OrdinalIgnoreCase));

   sealed record DirectoryCleanupJobs(WorktreeDirectoryCleanup[] Jobs);
}
