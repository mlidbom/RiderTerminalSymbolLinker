using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.Projects.Git;

namespace Cloyd.ProjectsDashboard;

/// <summary>Which project and worktree own each dashboard session. Reading the hub and Git again
/// reuses each surviving row, even when discovery gives its session a different parent.</summary>
sealed class DashboardHierarchy(ProjectWindows windows, DashboardWorktrees worktrees)
{
   readonly ProjectWindows _windows = windows;
   readonly DashboardWorktrees _worktrees = worktrees;
   readonly Dictionary<string, DashboardRow> _byKey = new(StringComparer.OrdinalIgnoreCase);
   readonly HashSet<DashboardRow> _present = [];

   public List<DashboardRow> Roots { get; } = [];
   public IEnumerable<DashboardProject> Projects => Roots.Cast<DashboardProject>();
   public IEnumerable<DashboardGroup> Groups => _byKey.Values.OfType<DashboardGroup>();
   public bool Contains(DashboardSession session) => _present.Contains(session);

   public void Refresh()
   {
      _present.Clear();
      Roots.Clear();
      foreach(var repository in _worktrees.Repositories) IncludeRepository(repository);
      foreach(var window in _windows.Open) GroupFor(window.Project, window.Project);
      foreach(var host in _windows.Sessions.Hosts) IncludeHost(host);
      foreach(var running in _windows.Sessions.Running.Where(it => it.SessionHost is null)) IncludeOutsideHost(running);
      foreach(var gone in _byKey.Values.Where(it => !_present.Contains(it)).ToArray()) _byKey.Remove(gone.Key);
      foreach(var project in Projects) project.UpdateStartFolder();
   }

   void IncludeRepository(GitWorktrees repository)
   {
      var project = ProjectGroup(repository.Repository);
      foreach(var checkout in repository.Checkouts) WorktreeIn(project, checkout);
   }

   void IncludeHost(SessionHostOfTheHub host)
   {
      var directory = new Project(host.Directory);
      var group = GroupFor(directory, host.Project ?? directory);
      var row = SessionIn(group, $"host:{host.Id}");
      var running = _windows.Sessions.Running.FirstOrDefault(it => it.SessionHost == host.Id);
      var known = _windows.Sessions.Known(host.Id);
      var latest = known?.LatestSession ?? row.Latest;
      if(latest is null && row.Host is null) latest = _windows.Sessions.Saved(host)?.Session;
      row.Update(host, running, latest, known?.KnownAgentCli);
   }

   void IncludeOutsideHost(RunningSession running)
   {
      var row = SessionIn(ProjectGroup(null), $"external:{running.Session.AgentCli.Name}:{running.Session.Id}");
      row.Update(null, running, running.Session, running.Session.AgentCli);
   }

   /// <summary>The host's working folder determines its worktree. A label can identify its project
   /// even when the shell works outside every checkout of that project.</summary>
   DashboardGroup GroupFor(Project folder, Project labelledProject)
   {
      var actual = _worktrees.Of(folder);
      if(actual?.Containing(folder) is {} checkout)
         return WorktreeIn(ProjectGroup(actual.Repository), checkout);
      return ProjectGroup((actual ?? _worktrees.Of(labelledProject))?.Repository ?? labelledProject);
   }

   DashboardProject ProjectGroup(Project? project) =>
      Include(DashboardProject.KeyFor(project), null, () => new DashboardProject(project));

   DashboardWorktree WorktreeIn(DashboardProject group, GitWorktree checkout)
   {
      var row = Include(DashboardWorktree.KeyFor(checkout.Root), group, () => new DashboardWorktree(checkout));
      row.Update(checkout);
      return row;
   }

   DashboardSession SessionIn(DashboardGroup group, string key) => Include(key, group, () => new DashboardSession(key));

   TRow Include<TRow>(string key, DashboardGroup? parent, Func<TRow> create) where TRow : DashboardRow
   {
      if(!_byKey.TryGetValue(key, out var row)) _byKey.Add(key, row = create());
      if(_present.Add(row))
      {
         row.Parent = parent;
         if(row is DashboardGroup group) group.Children.Clear();
         (parent?.Children ?? Roots).Add(row);
      }
      return (TRow)row;
   }
}
