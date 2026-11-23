using Cloyd.Hub.Worktrees;
using Cloyd.Projects;
using Cloyd.Projects.Git;

namespace Cloyd.ProjectsDashboard;

sealed partial class ProjectsDashboardWindow
{
   async Task RemoveWorktree(DashboardWorktree row)
   {
      var folder = row.StartFolder;
      try
      {
         var removal = new RemoveDashboardWorktree(Sessions.Hub, Sessions.Folder, row.Group.Project!, folder, sessions: Sessions, dashboardOccupants: DashboardOccupants);
         var dialog = new WorktreeRemovalDialog(removal, NameOf, checkout => WorktreeContextMenu.OpenGitExtensions(checkout, "commit"), Sessions.HasUnsentDraft, _closed.Token);
         dialog.ShellClosed += Sessions.HostWasClosed;
         await dialog.ShowDialog(this);
      }
      finally
      {
         if(!_closed.IsCancellationRequested) await RefreshAfterRemoval();
      }
   }

   IEnumerable<WorktreeOccupant> DashboardOccupants(Project root) =>
      Tree.Sessions.Where(it => it.Parent is DashboardWorktree group && group.StartFolder.Equals(root) && (it.HasHost || it.Running is not null))
          .Select(it => new WorktreeOccupant(new(it.Host?.Id.Value, null, it.Running?.Session.AgentCli == Cloyd.Sessions.AgentCli.ClaudeCode ? "claude" : it.Running?.Session.AgentCli.NameInTheHub,
             it.Running?.Session.Id.Value, null, null), "unknown", null, "This occupant is still listed in Projects Dashboard. Refresh after it closes.", it.Name));

   string NameOf(WorktreeSession session) => Tree.Sessions.FirstOrDefault(it =>
      it.Latest?.Id.Value == session.SessionId && (it.Latest.AgentCli.NameInTheHub == session.AgentCli ||
         (session.AgentCli == "claude" && it.Latest.AgentCli == Cloyd.Sessions.AgentCli.ClaudeCode)))?.Name ?? $"{session.AgentCli} · {session.SessionId}";

   async Task RefreshAfterRemoval()
   {
      await Sessions.Catalogue.Read(_closed.Token);
      await Sessions.Catalogue.Read(_closed.Token, force: true);
      await Tree.RefreshWorktrees(RefreshRows, Tell, _closed.Token);
   }
}
