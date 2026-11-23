using Cloyd.Hub;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects.Git;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard;

/// <summary>Both Git checkouts and the hub's independent inventory for each of them.</summary>
sealed record WorktreeRemovalReview(TheHub ReviewedHub, WorktreeRemovalPlan Git, WorktreeSessionsPreview[] Inventories, WorktreeOccupants[] Occupants, PendingWorktreeDisposition.Confirmation[] Pending)
{
   public int SavedCount => Inventories.Sum(it => it.Sessions.Count(session => !session.IsOpen));
   public HashSet<(string Folder, string Agent, string Id)> SavedIdentities => Inventories.SelectMany(inventory =>
      inventory.Sessions.Where(it => !it.IsOpen).Select(it => (inventory.WorktreePath, it.AgentCli, it.SessionId))).ToHashSet();
   public bool CanRemove => Pending.Length == 0 && Git.CanRemove && Occupants.All(it => it.Rows.Length == 0) && Inventories.All(it => !it.HasOpenTabsOrRunningSessions && it.Blockers.Length == 0);
}
