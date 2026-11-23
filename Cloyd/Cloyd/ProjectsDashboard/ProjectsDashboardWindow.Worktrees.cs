using System.Diagnostics;
using Cloyd.Projects.Git;
using Cloyd.Hub.Worktrees;

namespace Cloyd.ProjectsDashboard;

sealed partial class ProjectsDashboardWindow
{
   async Task RemoveWorktree(DashboardWorktree row, WorktreeSessionsPreview sessions)
   {
      var folder = row.StartFolder;
      if(!Sessions.BeginRemovingWorktree(folder)) return;
      try
      {
         var action = new RemoveDashboardWorktree(this, Sessions.Hub, Sessions.Folder, ShowUncommittedChanges);
         await action.Run(row.Group.Project!, folder, sessions, _closed.Token);
      }
      finally
      {
         Sessions.FinishRemovingWorktree(folder);
         if(!_closed.IsCancellationRequested) _ = ReportFailure(RefreshAfterRemoval);
      }
   }

   async Task RefreshAfterRemoval()
   {
      await Sessions.Catalogue.Read(_closed.Token);
      await Sessions.Catalogue.Read(_closed.Token, force: true);
      await Tree.RefreshWorktrees(RefreshRows, Tell, _closed.Token);
   }

   void ShowUncommittedChanges(WorktreeRemoval git)
   {
      Tell(new InvalidOperationException($"{git.Checkout.Root} has uncommitted changes. Review them in Git Extensions before removing the worktree."));
      using var window = Process.Start(WorktreeContextMenu.GitExtensions(git.Checkout.Root, "commit"));
   }
}
