using Cloyd.Hub;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects.Git;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard;

/// <summary>Both Git checkouts and the hub's independent inventory for each of them.</summary>
sealed class WorktreeRemovalReview
{
   public WorktreeRemovalReview(TheHub reviewedHub, WorktreeRemovalPlan git, WorktreeSessionsPreview[] inventories, WorktreeOccupants[] occupants, PendingWorktreeDisposition.Confirmation[] pending)
   {
      ReviewedHub = reviewedHub;
      Git = git;
      Inventories = inventories;
      Pending = pending;
      Subagents = new WorktreeSubagents(occupants);
      foreach(var inventory in inventories) Subagents.RequireSeparateArchiveTargets(inventory);
      Occupants = occupants.Select(Subagents.Reconcile).ToArray();
   }
   public TheHub ReviewedHub { get; }
   public WorktreeRemovalPlan Git { get; }
   public WorktreeSessionsPreview[] Inventories { get; }
   public WorktreeOccupants[] Occupants { get; }
   public PendingWorktreeDisposition.Confirmation[] Pending { get; }
   public WorktreeSubagents Subagents { get; }
   public int SavedCount => Inventories.Sum(it => it.Sessions.Count(session => !session.IsOpen));
   public HashSet<(string Folder, string Agent, string Id)> SavedIdentities => Inventories.SelectMany(inventory =>
      inventory.Sessions.Where(it => !it.IsOpen).Select(it => (inventory.WorktreePath, it.AgentCli, it.SessionId))).ToHashSet();
   public bool CanRemove => Pending.Length == 0 && Git.CanRemove && Occupants.All(it => it.Rows.Length == 0) && !Subagents.Blockers.Any() &&
                            Inventories.All(it => !it.HasOpenTabsOrRunningSessions && it.Blockers.Length == 0);
}
