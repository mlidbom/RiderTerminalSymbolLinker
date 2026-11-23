using Cloyd.Projects;

namespace Cloyd.Hub.Worktrees;

/// <summary>The hub's receipt for cleanup of one particular worktree directory. Its ID is bound
/// to the directory's native identity and repository while the worktree is still registered.</summary>
sealed record WorktreeDirectoryCleanup(Guid CleanupId, string WorktreePath, string Status, string? Reason = null)
{
   public void Require(Project worktree, Guid? expectedId, params string[] statuses)
   {
      if(CleanupId == Guid.Empty || (expectedId.HasValue && CleanupId != expectedId) || !Path.IsPathFullyQualified(WorktreePath) ||
         !worktree.Equals(new Project(WorktreePath)) || !statuses.Contains(Status, StringComparer.Ordinal))
         throw new InvalidDataException("The hub returned a directory cleanup receipt for a different job, path or state.");
   }
}
