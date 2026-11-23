using Cloyd.Hub;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.Saving;
using Cloyd.Sessions;

namespace Cloyd.ProjectsDashboard;

/// <summary>Reviews Git and sessions before removing either checkout. A confirmed session disposition
/// must have a complete receipt before Git can remove the shared rules or the project.</summary>
sealed class RemoveDashboardWorktree(TheHub hub, CloydsFolder cloydFolder, Project project, Project worktree, SharedRulesCatalog? catalog = null,
                                     SessionsOfCloyd? sessions = null, Func<Project, IEnumerable<WorktreeOccupant>>? dashboardOccupants = null)
{
   public Project Worktree => worktree;
   public Task<WorktreeRemovalPlan> ReadGit(CancellationToken cancellation) => WorktreeRemovalPlan.Read(project, worktree, cancellation, catalog);

   public async Task<WorktreeRemovalReview> ReviewSessions(WorktreeRemovalPlan git, CancellationToken cancellation)
   {
      var source = await hub.ForOneGeneration(cancellation);
      var inventories = await Task.WhenAll(git.Checkouts.Select(async checkout =>
      {
         var inventory = await source.PreviewWorktreeSessions(checkout.Checkout.Root, cancellation);
         inventory.RequireIdentityOf(checkout.Checkout.Root);
         return inventory;
      }));
      var occupants = await Task.WhenAll(inventories.Select(async inventory =>
      {
         var root = new Project(inventory.WorktreePath);
         var eligibility = await source.ReviewWorktreeOccupants(root, cancellation);
         var local = await LiveDashboardOccupants(source, root, cancellation);
         return WorktreeOccupants.From(inventory, eligibility, local);
      }));
      var pending = git.Checkouts.Select(it => new PendingWorktreeDisposition(cloydFolder, it.Checkout.Root).AwaitingReceipt)
                                 .OfType<PendingWorktreeDisposition.Confirmation>().ToArray();
      return new(source, git, inventories, occupants, pending);
   }

   /// <summary>The tree retains a host's last worktree when the hub cannot identify its foreground session.
   /// Its periodic reading may still contain a tab or process that just closed.</summary>
   async Task<WorktreeOccupant[]> LiveDashboardOccupants(TheHub source, Project root, CancellationToken cancellation)
   {
      var local = dashboardOccupants?.Invoke(root).ToArray() ?? [];
      if(local.Length == 0) return [];
      var hosts = local.Any(it => it.Target.HostId is not null) ? await source.SessionHosts(cancellation) : [];
      var agents = local.Any(it => it.Target.HostId is null) ? await source.RunningSessions(cancellation) : [];
      return local.Where(it => it.Target.HostId is {} host ? hosts.Any(known => known.Id.Value == host) :
         agents.Any(agent => agent.Session.Id.Value == it.Target.SessionId && (agent.Session.AgentCli == AgentCli.ClaudeCode ? "claude" : agent.Session.AgentCli.NameInTheHub) == it.Target.AgentCli)).ToArray();
   }

   public async Task<string?> Recover(PendingWorktreeDisposition.Confirmation previous)
   {
      await new PendingWorktreeDisposition(cloydFolder, previous.Worktree).Reconcile(hub, previous.Operation);
      return "The previous confirmation was reconciled. Review the remaining worktree contents.";
   }

   public async Task Remove(WorktreeRemovalReview reviewed, IReadOnlySet<Project> deleteBranches, bool deleteConversations, CancellationToken cancellation)
   {
      if(sessions?.BeginRemovingWorktree(worktree) == false) throw new InvalidOperationException("Another removal is already in progress.");
      try { await RemoveReviewed(reviewed, deleteBranches, deleteConversations, cancellation); }
      finally { sessions?.FinishRemovingWorktree(worktree); }
   }

   async Task RemoveReviewed(WorktreeRemovalReview reviewed, IReadOnlySet<Project> deleteBranches, bool deleteConversations, CancellationToken cancellation)
   {
      if(!reviewed.CanRemove) throw new InvalidOperationException("Close live sessions and resolve Git changes before removal.");
      await RequireReviewedGit();
      // Check both inventories before changing either. Apply uses the operation ID from the displayed review.
      var current = await ReviewSessions(reviewed.Git, cancellation);
      if(!current.CanRemove) throw new InvalidOperationException("A session or shell now uses the worktree. Review its occupants again.");
      foreach(var before in reviewed.Inventories)
      {
         var root = new Project(before.WorktreePath);
         var after = current.Inventories.Single(it => new Project(it.WorktreePath).Equals(root));
         after.RequireReadyFor(root);
         if(!before.Sessions.ToHashSet().SetEquals(after.Sessions))
            throw new InvalidOperationException("The worktree's sessions changed. Refresh and review them again.");
      }
      var processed = 0;
      try
      {
         foreach(var inventory in reviewed.Inventories)
         {
            cancellation.ThrowIfCancellationRequested();
            var folder = new Project(inventory.WorktreePath);
            var pending = new PendingWorktreeDisposition(cloydFolder, folder);
            if(pending.AwaitingReceipt is not null) throw new InvalidOperationException("Recover the previously confirmed session disposition first.");
            var result = await pending.Apply(hub, inventory, inventory.Empty ? null : deleteConversations ? "recoverableDelete" : "archive");
            processed += result.Results.Length;
         }
         await RequireReviewedGit();
         await reviewed.Git.Remove(deleteBranches, RequireVacant, cancellation);
      }
      catch(Exception failure) when(processed > 0)
      {
         throw new InvalidOperationException($"{processed} saved conversations processed. {failure.Message}", failure);
      }

      async Task RequireReviewedGit()
      {
         if(await ReadGit(cancellation) != reviewed.Git) throw new InvalidOperationException("Git state changed. Refresh and review it again.");
      }
      async Task RequireVacant()
      {
         foreach(var checkout in reviewed.Git.Checkouts.Where(it => Directory.Exists(it.Checkout.Root.Folder)))
         {
            var remaining = await hub.PreviewWorktreeSessions(checkout.Checkout.Root, cancellation);
            remaining.RequireReadyFor(checkout.Checkout.Root);
            if(!remaining.Empty || (await LiveDashboardOccupants(hub, checkout.Checkout.Root, cancellation)).Length != 0)
               throw new InvalidOperationException($"New sessions were found in {checkout.Checkout.Root}. Review them before removal.");
         }
      }
   }
}
