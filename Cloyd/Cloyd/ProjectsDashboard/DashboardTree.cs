using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using Cloyd.Projects;
using Cloyd.ProjectsDashboard.Filters;
using Cloyd.Saving;
using Cloyd.Sessions;

namespace Cloyd.ProjectsDashboard;

/// <summary>The tab strip's selection and visible rows. Refreshing membership or rearranging a group
/// keeps the same <see cref="ActiveSession"/> and its view while the session remains in the hierarchy.</summary>
sealed class DashboardTree : ObservableObject
{
   readonly ProjectWindows _windows;
   readonly DashboardWorktrees _worktrees = new();
   readonly DashboardHierarchy _hierarchy;
   readonly ClosedProjectTabs _closedTabs;
   readonly DashboardArrangement _arrangement = new();
   readonly DashboardGitStatuses _gitStatuses = new();
   DashboardSearch _search = new("");

   public DashboardTree(ProjectWindows windows)
   {
      _windows = windows;
      Filters = new DashboardSessionFilters(_arrangement.Saved);
      _closedTabs = new ClosedProjectTabs(windows);
      _hierarchy = new DashboardHierarchy(windows, _worktrees, _closedTabs);
   }

   public IEnumerable<DashboardProject> Projects => _hierarchy.Projects;
   public IEnumerable<DashboardSession> Sessions => RowsInOrder(_hierarchy.Roots, expandAll: true, filter: false).OfType<DashboardSession>();
   public IEnumerable<DashboardSession> NavigableSessions => Sessions.Where(Includes);
   [AllowNull] public string SearchText
   {
      get => field;
      set
      {
         value ??= "";
         if(!SetProperty(ref field, value)) return;
         _search = new DashboardSearch(value);
         foreach(var group in _hierarchy.Groups) group.ExpandedDuringSearch = _search.IsActive ? true : null;
         RefreshFilter();
      }
   } = "";
   bool Includes(DashboardSession session) => Filters.Includes(session) && _search.Matches(session);
   public ObservableCollection<DashboardRow> VisibleRows { get; } = [];
   public SavedDashboardArrangement Arrangement
   {
      get => _arrangement.Saved;
      set
      {
         _arrangement.Saved = value;
         Filters = new DashboardSessionFilters(value);
         NotifyFiltersChanged();
      }
   }
   public DashboardSessionFilters Filters { get; private set; }
   public event Action? FiltersChanged;
   public bool EditingFilters
   {
      get => field;
      set { if(SetProperty(ref field, value)) ShowExpandedGroups(); }
   }
   public bool NoMatches => VisibleRows.Count == 0;
   public void ToggleInclusion(DashboardGroup? group, IncludedSessions kind)
   {
      Filters.Toggle(group, kind);
      NotifyFiltersChanged();
   }
   public void UseParentSetting(DashboardGroup group, IncludedSessions kind)
   {
      Filters.Inherit(group, kind);
      NotifyFiltersChanged();
   }
   void NotifyFiltersChanged()
   {
      FiltersChanged?.Invoke();
      RefreshFilter();
   }
   public DashboardSession? ActiveSession { get; private set; }
   public event Action? ActiveSessionChanged;

   public Task DiscoverWorktrees(Action refreshed, Action<Exception> failed, CancellationToken cancellation) =>
      _worktrees.Discover(_windows, Sessions.Select(it => it.WorkingFolder).OfType<Project>(), Arrangement, refreshed, failed, cancellation);

   /// <summary>A scoped history reading may introduce folders while an earlier Git discovery is underway.
   /// Finish that discovery and include the new folders before taking the heading's membership.</summary>
   public async Task DiscoverHistoryWorktrees(Action refreshed, Action<Exception> failed, CancellationToken windowClosed, CancellationToken pickerClosed)
   {
      await _worktrees.CurrentReading.WaitAsync(pickerClosed);
      pickerClosed.ThrowIfCancellationRequested();
      await DiscoverWorktrees(refreshed, failed, windowClosed).WaitAsync(pickerClosed);
   }

   public Task ReadGitStatus(CancellationToken cancellation) => _gitStatuses.Refresh(_worktrees.Repositories, ShowGitStatus, cancellation);

   public Task ReadClosedProjectTabs(Action refreshed, CancellationToken cancellation) => _closedTabs.Read(refreshed, cancellation);

   void ShowGitStatus()
   {
      foreach(var worktree in _hierarchy.Groups.OfType<DashboardWorktree>()) worktree.ShowGitStatus(_gitStatuses);
   }

   public void Refresh()
   {
      _hierarchy.Refresh();
      ShowGitStatus();
      ReadPins();
      _arrangement.Restore(_hierarchy);
      foreach(var group in _hierarchy.Groups)
      {
         if(_search.IsActive) group.ExpandedDuringSearch ??= true;
         group.Summarize(Includes);
      }
      if(ActiveSession is {} active && !_hierarchy.Contains(active)) Select(active.Latest is {} latest ? Find(latest.Id) : null);
      else ShowExpandedGroups();
   }

   public DashboardSession? Find(SessionId id) => Sessions.Where(it => it.Latest?.Id == id)
                                                          .OrderByDescending(it => it.Running is not null).FirstOrDefault();

   public void TogglePin(DashboardSession session)
   {
      if(!Arrangement.PinnedSessions.Remove(session.PinKey))
      {
         if(session.Host is {} host) _windows.Sessions.RememberPinnedHost(host, session.Latest, session.WorkingFolder);
         Arrangement.PinnedSessions.Add(session.PinKey);
      }
      ReadPins();
      RefreshFilter();
   }

   void ReadPins()
   {
      var running = _windows.Sessions.Running.Select(it => it.Session.Id).ToHashSet();
      foreach(var row in Sessions)
         if(row.ReadPin(Arrangement.PinnedSessions, running) && row.Host is {} host)
            _windows.Sessions.RememberPinnedHost(host, row.Latest, row.WorkingFolder);
   }

   void RefreshFilter()
   {
      foreach(var group in _hierarchy.Groups) group.Summarize(Includes);
      ShowExpandedGroups();
   }

   public void Select(DashboardSession? row)
   {
      if(ActiveSession == row) return;
      if(ActiveSession is {} old) old.IsActive = false;
      ActiveSession = row;
      if(row is not null) row.IsActive = true;
      ShowExpandedGroups();
      ActiveSessionChanged?.Invoke();
   }

   public void Toggle(DashboardGroup group)
   {
      if(_search.IsActive) group.ExpandedDuringSearch = !group.ShownExpanded;
      else _arrangement.Toggle(group);
      group.Summarize(Includes);
      ShowExpandedGroups();
   }

   public void Move(DashboardRow row, DashboardRow neighbour, bool after)
   {
      _arrangement.Move(_hierarchy, row, neighbour, after);
      ShowExpandedGroups();
   }

   /// <summary>A collapsed group exposes just the active tab beneath its heading.
   /// Reuse existing row containers whenever their place in the visible list stays the same.</summary>
   void ShowExpandedGroups()
   {
      var wanted = RowsInOrder(_hierarchy.Roots, expandAll: false, filter: true).ToArray();
      for(var i = 0; i < wanted.Length; i++)
      {
         if(i < VisibleRows.Count && VisibleRows[i] == wanted[i]) continue;
         var old = VisibleRows.IndexOf(wanted[i]);
         if(old >= 0) VisibleRows.Move(old, i);
         else VisibleRows.Insert(i, wanted[i]);
      }
      while(VisibleRows.Count > wanted.Length) VisibleRows.RemoveAt(VisibleRows.Count - 1);
      OnPropertyChanged(nameof(NoMatches));
   }

   /// <summary>Walks the arranged rows in the same order for the tab strip and Ctrl+Tab.
   /// Navigation also visits the sessions hidden under a collapsed heading.</summary>
   IEnumerable<DashboardRow> RowsInOrder(IEnumerable<DashboardRow> rows, bool expandAll, bool filter)
   {
      foreach(var row in rows)
      {
         if(filter && row is DashboardSession session && !Includes(session)) continue;
         if(filter && row is DashboardGroup heading && !ShowsHeading(heading)) continue;
         yield return row;
         if(row is not DashboardGroup group) continue;
         if(expandAll || group.ShownExpanded)
         {
            foreach(var child in RowsInOrder(group.Children, expandAll, filter)) yield return child;
         }
         else if(ActiveSession is {} active && (!filter || Includes(active)) && active.BelongsTo(group)) yield return active;
      }
   }

   bool ShowsHeading(DashboardGroup group) =>
      (EditingFilters || Filters.ShowsEmpty(group)) && _search.Matches(group)
      || group.Children.Any(it => it is DashboardSession session ? Includes(session) : ShowsHeading((DashboardGroup)it));
}
