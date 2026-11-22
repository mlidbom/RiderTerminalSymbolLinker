using Cloyd.ClaudeCode;
using Cloyd.ProjectsDashboard;
using Cloyd.Sessions;

namespace Cloyd.Projects;

/// <summary>
/// The <see cref="ProjectWindow"/>s the Cloyd application shows: one for each project, on the UI thread. A window is one
/// of them from when it opens until it closes, and the project's next one opens anew.
/// </summary>
/// <param name="newWindowOf">Makes a new window of a project, one of these windows once it opens, not yet shown.</param>
/// <param name="sessions">The application’s shared sessions.</param>
sealed class ProjectWindows(Func<Project, ProjectWindows, ProjectWindow> newWindowOf, SessionsOfCloyd sessions)
{
   readonly Func<Project, ProjectWindows, ProjectWindow> _newWindowOf = newWindowOf;
   readonly List<ProjectWindow> _open = [];
   public SessionsOfCloyd Sessions { get; } = sessions;
   public ProjectsDashboardWindow? Dashboard { get; private set; }

   public ProjectsDashboardWindow ShowDashboard(Project openedFrom)
   {
      if(Dashboard is {} open)
      {
         open.BringForward();
         return open;
      }

      var dashboard = new ProjectsDashboardWindow(this, openedFrom);
      Dashboard = dashboard;
      Sessions.WindowOpened();
      dashboard.Closed += (_, _) =>
      {
         Dashboard = null;
         Sessions.WindowClosed();
      };
      dashboard.Show();
      return dashboard;
   }


   /// <summary>The windows open, in the order they opened.</summary>
   public IReadOnlyList<ProjectWindow> Open => _open;

   /// <summary>Where a coordinator's helper will be visible, keeping Magnus's active window in front.</summary>
   public ProjectWindow ForAPeer(Project project)
   {
      if(_open.SingleOrDefault(it => it.Project.Equals(project)) is {} existing) return existing;
      var window = _newWindowOf(project, this);
      window.ShowActivated = false;
      window.Show();
      return window;
   }

   /// <summary>
   /// Shows the window of <paramref name="project"/>, as an IDE opening the project asks: brings it forward when it is
   /// open on the virtual desktop shown, else opens a new one. One open on another desktop, as its IDE window's
   /// (<see cref="ProjectWindow.Desktop"/>), stays where it is: bringing it forward would switch Magnus to
   /// that desktop, as an IDE restarting would, opening its projects again.
   /// </summary>
   public ProjectWindow Show(Project project)
   {
      if(_open.SingleOrDefault(it => it.Project.Equals(project)) is {} open)
      {
         if(open.IsOnTheCurrentVirtualDesktop) open.BringForward();
         return open;
      }

      var window = _newWindowOf(project, this);
      window.Show();
      return window;
   }

   /// <summary>Takes <paramref name="window"/>, which has just opened, as one of the windows open, until it closes.</summary>
   public void Opened(ProjectWindow window)
   {
      _open.Add(window);
      Sessions.WindowOpened();
      window.Closed += (_, _) => { _open.Remove(window); Sessions.WindowClosed(); };
   }

   /// <summary>
   /// Shows the tab whose Claude Code runs as <paramref name="claudeCode"/>, else the one that runs
   /// <paramref name="session"/> or ran it last: selects it, and brings its window forward. Returns the window: null
   /// when no open window has such a tab.
   /// </summary>
   /// <remarks>
   /// As a click on a notification of the session asks: on whatever virtual desktop the window is, Windows switches to it.
   /// The process finds the tab after a <c>/clear</c> gave its Claude Code a new session, and the session finds the one
   /// whose Claude Code exited to the shell.
   /// </remarks>
   public ProjectWindow? ShowTheTabOf(ClaudeCodeSessionId? session, ClaudeCodeProcessId? claudeCode)
   {
      var tabs = _open.SelectMany(it => it.Tabs).ToList();
      if((TheTabWhoseClaudeCodeRunsAs(claudeCode) ?? TheTabThatRunsOrRanLast(session)) is not {} tab) return null;

      var window = _open.Single(it => it.Tabs.Contains(tab));
      window.ShowTheTab(tab);
      return window;

      SessionTab? TheTabWhoseClaudeCodeRunsAs(ClaudeCodeProcessId? process) => process is {} asked ? tabs.FirstOrDefault(it => it.RunsTheClaudeCodeOf(asked)) : null;

      SessionTab? TheTabThatRunsOrRanLast(ClaudeCodeSessionId? sessionId) => sessionId is {} asked ? tabs.FirstOrDefault(it => it.RunsOrRanLast(asked)) : null;
   }
}
