using Avalonia.Controls;
using Cloyd.Hub;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects;
using Cloyd.Projects.Git;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard;

/// <summary>Coordinates Magnus's Git and conversation decisions for one linked checkout.
/// Both decisions precede the hub's guarded closure of idle sessions and Git's removal.</summary>
sealed class RemoveDashboardWorktree(Window owner, TheHub hub, CloydsFolder cloydFolder, Action<WorktreeRemoval> showChanges, Action<SessionHostId> forgetHost)
{
   public async Task Run(Project repository, Project folder, WorktreeSessionsPreview sessions, CancellationToken cancellation)
   {
      sessions.RequireReadyFor(folder);
      var git = await WorktreeRemoval.Inspect(repository, folder, cancellation);
      if(git.Status.ChangedPaths > 0) { showChanges(git); return; }
      var pending = new PendingWorktreeDisposition(cloydFolder, folder);
      if(await pending.Reconcile(hub, forgetHost))
      {
         sessions = await hub.PreviewWorktreeSessions(folder, cancellation);
         sessions.RequireReadyFor(folder);
      }
      if(git.Status.Integrated != true)
      {
         var warning = $"{git.Status.Explanation}. Remove the worktree at {folder}?";
         warning += git.Checkout.Branch is {} branch ? $" The branch {branch} will remain." : $" Its detached commit is {git.Commit}.";
         if(await WorktreeRemovalDialog.Warn(owner, warning) != WorktreeRemovalDialog.Choice.Continue) return;
      }
      var choice = await ChooseDisposition(sessions);
      if(choice == WorktreeRemovalDialog.Choice.Cancel) return;
      var checkedGit = await WorktreeRemoval.Inspect(repository, folder, cancellation);
      if(checkedGit.Status.ChangedPaths > 0) { showChanges(checkedGit); return; }
      if(checkedGit != git) throw new InvalidOperationException("The checkout changed while the dialog was open. Review it again.");
      var disposition = choice switch { WorktreeRemovalDialog.Choice.Archive => "archive", WorktreeRemovalDialog.Choice.Delete => "recoverableDelete", _ => null };
      cancellation.ThrowIfCancellationRequested();
      var result = await pending.Apply(hub, sessions, disposition, forgetHost);
      try { await git.Remove(cancellation, RequireStillVacant); }
      catch(Exception failure) when(!cancellation.IsCancellationRequested)
      {
         if(Directory.Exists(folder.Folder) && (await GitStatusReport.Read(folder, cancellation)).ChangedPaths > 0) showChanges(git);
         throw new InvalidOperationException($"Git could not finish removing {folder}. The hub processed {result.Results.Length} sessions and closed {result.ClosedHostIds.Length} shells. {failure.Message}", failure);
      }

      async Task RequireStillVacant()
      {
         var remaining = await hub.PreviewWorktreeSessions(folder, cancellation);
         remaining.RequireReadyFor(folder);
         if(!remaining.Empty) throw new InvalidOperationException("A session or shell now uses this worktree. Review it again.");
      }
   }

   async Task<WorktreeRemovalDialog.Choice> ChooseDisposition(WorktreeSessionsPreview sessions)
   {
      if(sessions.Sessions.Length > 0) return await WorktreeRemovalDialog.AskAboutSessions(owner, sessions.DescribeRemoval());
      if(sessions.Empty) return WorktreeRemovalDialog.Choice.Continue;
      var count = sessions.ShellOnlyHosts.Length;
      return await WorktreeRemovalDialog.Warn(owner, $"Close {count} idle {(count == 1 ? "shell" : "shells")} and remove {sessions.WorktreePath}?");
   }
}
