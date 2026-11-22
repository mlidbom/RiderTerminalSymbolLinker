using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Cloyd.Projects;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard;

/// <summary>The tab strip's selection and visible rows. Refreshing membership or rearranging a group
/// keeps the same <see cref="ActiveSession"/> and its view while the session remains in the hierarchy.</summary>
sealed class DashboardTree : ObservableObject
{
   readonly ProjectWindows _windows;
   readonly DashboardWorktrees _worktrees = new();
   readonly DashboardHierarchy _hierarchy;
   readonly DashboardArrangement _arrangement = new();

   public DashboardTree(ProjectWindows windows)
   {
      _windows = windows;
      _hierarchy = new DashboardHierarchy(windows, _worktrees);
   }

   public IEnumerable<DashboardProject> Projects => _hierarchy.Projects;
   public IEnumerable<DashboardSession> Sessions => RowsInOrder(_hierarchy.Roots, expandAll: true, onlyPinned: false).OfType<DashboardSession>();
   public IEnumerable<DashboardSession> NavigableSessions => Sessions.Where(it => !OnlyPinned || it.MatchesPinFilter);
   public ObservableCollection<DashboardRow> VisibleRows { get; } = [];
   public SavedDashboardArrangement Arrangement
   {
      get => _arrangement.Saved;
      set { _arrangement.Saved = value; OnPropertyChanged(nameof(OnlyPinned)); }
   }
   public bool OnlyPinned
   {
      get => Arrangement.OnlyPinned;
      set
      {
         if(Arrangement.OnlyPinned == value) return;
         Arrangement.OnlyPinned = value;
         OnPropertyChanged();
         RefreshPinFilter();
      }
   }
   public bool ShowNoPinnedSessions => OnlyPinned && VisibleRows.Count == 0;
   public DashboardSession? ActiveSession { get; private set; }
   public event Action? ActiveSessionChanged;

   public Task DiscoverWorktrees(Action refreshed, Action<Exception> failed, CancellationToken cancellation) =>
      _worktrees.Discover(_windows, Sessions.Select(it => it.WorkingFolder).OfType<Project>(), Arrangement, refreshed, failed, cancellation);

   public void Refresh()
   {
      _hierarchy.Refresh();
      ReadPins();
      _arrangement.Restore(_hierarchy);
      if(ActiveSession is {} active && !_hierarchy.Contains(active)) Select(null);
      else ShowExpandedGroups();
   }

   public void TogglePin(DashboardSession session)
   {
      if(!Arrangement.PinnedSessions.Remove(session.PinKey))
      {
         if(session.Host is {} host) _windows.Sessions.RememberPinnedHost(host, session.Latest, session.WorkingFolder);
         Arrangement.PinnedSessions.Add(session.PinKey);
      }
      ReadPins();
      RefreshPinFilter();
   }

   void ReadPins()
   {
      var running = _windows.Sessions.Running.Select(it => it.Session.Id).ToHashSet();
      foreach(var row in Sessions)
         if(row.ReadPin(Arrangement.PinnedSessions, running) && row.Host is {} host)
            _windows.Sessions.RememberPinnedHost(host, row.Latest, row.WorkingFolder);
   }

   void RefreshPinFilter()
   {
      foreach(var group in _hierarchy.Groups) group.Summarize(OnlyPinned);
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
      _arrangement.Toggle(group);
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
      var wanted = RowsInOrder(_hierarchy.Roots, expandAll: false, OnlyPinned).ToArray();
      for(var i = 0; i < wanted.Length; i++)
      {
         if(i < VisibleRows.Count && VisibleRows[i] == wanted[i]) continue;
         var old = VisibleRows.IndexOf(wanted[i]);
         if(old >= 0) VisibleRows.Move(old, i);
         else VisibleRows.Insert(i, wanted[i]);
      }
      while(VisibleRows.Count > wanted.Length) VisibleRows.RemoveAt(VisibleRows.Count - 1);
      OnPropertyChanged(nameof(ShowNoPinnedSessions));
   }

   /// <summary>Walks the arranged rows in the same order for the tab strip and Ctrl+Tab.
   /// Navigation also visits the sessions hidden under a collapsed heading.</summary>
   IEnumerable<DashboardRow> RowsInOrder(IEnumerable<DashboardRow> rows, bool expandAll, bool onlyPinned)
   {
      foreach(var row in rows)
      {
         if(onlyPinned && (row is DashboardSession { MatchesPinFilter: false } || row is DashboardGroup filtered && !filtered.Sessions.Any(it => it.MatchesPinFilter))) continue;
         yield return row;
         if(row is not DashboardGroup group) continue;
         if(expandAll || group.Expanded)
         {
            foreach(var child in RowsInOrder(group.Children, expandAll, onlyPinned)) yield return child;
         }
         else if(ActiveSession is {} active && (!onlyPinned || active.MatchesPinFilter) && active.BelongsTo(group)) yield return active;
      }
   }
}
