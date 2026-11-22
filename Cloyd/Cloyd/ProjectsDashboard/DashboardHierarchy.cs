using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.History;
using Cloyd.Sessions;

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
      var listings = _windows.Sessions.Catalogue.WithRunning(_windows.Sessions.Running);
      var openTabs = _windows.Open.SelectMany(it => it.Tabs).Select(it => it.Host.Id).ToHashSet();
      foreach(var repository in _worktrees.Repositories) IncludeRepository(repository);
      foreach(var project in _worktrees.TrustedProjects) GroupFor(project, project);
      foreach(var window in _windows.Open) GroupFor(window.Project, window.Project);
      foreach(var host in _windows.Sessions.Hosts) IncludeHost(host, listings, openTabs.Contains(host.Id));
      foreach(var running in _windows.Sessions.Running.Where(it => it.SessionHost is null))
         if(listings.TryGetValue(running.Session.Id, out var listing)) IncludeOutsideHost(running, listing);
      IncludeRecordedSessions(listings);
      foreach(var gone in _byKey.Values.Where(it => !_present.Contains(it)).ToArray()) _byKey.Remove(gone.Key);
      foreach(var project in Projects) project.UpdateStartFolder();
   }

   void IncludeRecordedSessions(IReadOnlyDictionary<SessionId, SessionListing> listings)
   {
      var represented = _present.OfType<DashboardSession>().Select(it => it.Latest?.Id).OfType<SessionId>().ToHashSet();
      foreach(var listing in listings.Values.Where(it => !represented.Contains(it.Session.Id)))
      {
         var folder = FolderOf(listing);
         var group = RecordedGroup(folder);
         var row = SessionIn(group, $"recorded:{listing.Session.AgentCli.NameInTheHub}:{listing.Session.Id}");
         row.Update(null, listing.Running, listing.Session, listing.Session.AgentCli, folder, listing);
      }
   }

   static Project? FolderOf(SessionListing? listing) =>
      listing?.Directory is {} path && Path.IsPathFullyQualified(path) ? new Project(path) : null;

   DashboardGroup RecordedGroup(Project? folder)
   {
      if(folder is null) return ProjectGroup(null);
      if(_worktrees.Of(folder) is not null) return GroupFor(folder, folder);
      var containing = _worktrees.Repositories.Select(it => (Repository: it, Checkout: it.Containing(folder)))
                                  .Where(it => it.Checkout is not null).OrderByDescending(it => it.Checkout!.Root.Folder.Length).FirstOrDefault();
      if(containing.Checkout is {} checkout) return WorktreeIn(ProjectGroup(containing.Repository.Repository), checkout);
      var project = _worktrees.TrustedProjects.Concat(_windows.Open.Select(it => it.Project)).Where(it => it.Contains(folder)).MaxBy(it => it.Folder.Length);
      return ProjectGroup(project ?? folder);
   }

   void IncludeRepository(GitWorktrees repository)
   {
      var project = ProjectGroup(repository.Repository);
      foreach(var checkout in repository.Checkouts) WorktreeIn(project, checkout);
   }

   void IncludeHost(SessionHostOfTheHub host, IReadOnlyDictionary<SessionId, SessionListing> listings, bool open)
   {
      var key = $"host:{host.Id}";
      var previous = _byKey.GetValueOrDefault(key) as DashboardSession;
      var running = _windows.Sessions.Running.FirstOrDefault(it => it.SessionHost == host.Id);
      var known = _windows.Sessions.Known(host.Id);
      var saved = previous is null && known is null ? _windows.Sessions.Saved(host) : null;
      var latest = running?.Session ?? known?.LatestSession ?? previous?.Latest ?? saved?.Session;
      var listing = latest is null ? null : listings.GetValueOrDefault(latest.Id);
      // A shell stays available when its previous session resumes elsewhere.
      if(running is null && listing?.Running is not null) { latest = null; listing = null; }
      var lastFolder = running is null || running.Session.Id == previous?.Latest?.Id ? previous?.WorkingFolder : null;
      var folder = running?.WorkingFolder ?? known?.LastSessionFolder ?? FolderOf(listing) ?? lastFolder ?? new Project(saved?.SessionDirectory ?? host.Directory);
      var row = SessionIn(GroupFor(folder, host.Project ?? folder), key);
      row.Update(host, running, latest, latest is null ? null : known?.KnownAgentCli, folder, listing, open);
   }

   void IncludeOutsideHost(RunningSession running, SessionListing listing)
   {
      var folder = running.WorkingFolder ?? FolderOf(listing);
      var group = RecordedGroup(folder);
      var row = SessionIn(group, $"external:{running.Session.AgentCli.Name}:{running.Session.Id}");
      row.Update(null, running, running.Session, running.Session.AgentCli, folder, listing);
   }

   /// <summary>The host's working folder determines its worktree. A label can identify its project
   /// even when the shell works outside every checkout of that project.</summary>
   DashboardGroup GroupFor(Project folder, Project labelledProject)
   {
      var actual = _worktrees.Of(folder);
      if(actual?.Containing(folder) is {} checkout)
         return WorktreeIn(ProjectGroup(actual.Repository), checkout);
      if(actual is null && !folder.Equals(labelledProject))
      {
         var projects = _worktrees.TrustedProjects.Concat(_windows.Open.Select(it => it.Project));
         if(projects.Where(it => _worktrees.Of(it) is null && it.Contains(folder)).MaxBy(it => it.Folder.Length) is {} containing)
            return ProjectGroup(containing);
      }
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
