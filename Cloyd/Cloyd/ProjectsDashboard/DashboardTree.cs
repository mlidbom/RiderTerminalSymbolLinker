using System.Collections.ObjectModel;
using Cloyd.Projects;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard;

/// <summary>The tab strip's selection and visible rows. Refreshing membership or rearranging a group
/// keeps the same <see cref="ActiveSession"/> and its view while the session remains in the hierarchy.</summary>
sealed class DashboardTree
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
   public IEnumerable<DashboardSession> Sessions => RowsInOrder(_hierarchy.Roots, expandAll: true).OfType<DashboardSession>();
   public ObservableCollection<DashboardRow> VisibleRows { get; } = [];
   public SavedDashboardArrangement Arrangement { get => _arrangement.Saved; set => _arrangement.Saved = value; }
   public DashboardSession? ActiveSession { get; private set; }
   public event Action? ActiveSessionChanged;

   public Task DiscoverWorktrees(Action refreshed, Action<Exception> failed, CancellationToken cancellation) =>
      _worktrees.Discover(_windows, Arrangement, refreshed, failed, cancellation);

   public void Refresh()
   {
      _hierarchy.Refresh();
      _arrangement.Restore(_hierarchy);
      if(ActiveSession is {} active && !_hierarchy.Contains(active)) Select(null);
      else ShowExpandedGroups();
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
      var wanted = RowsInOrder(_hierarchy.Roots, expandAll: false).ToArray();
      for(var i = 0; i < wanted.Length; i++)
      {
         if(i < VisibleRows.Count && VisibleRows[i] == wanted[i]) continue;
         var old = VisibleRows.IndexOf(wanted[i]);
         if(old >= 0) VisibleRows.Move(old, i);
         else VisibleRows.Insert(i, wanted[i]);
      }
      while(VisibleRows.Count > wanted.Length) VisibleRows.RemoveAt(VisibleRows.Count - 1);
   }

   /// <summary>Walks the arranged rows in the same order for the tab strip and Ctrl+Tab.
   /// Navigation also visits the sessions hidden under a collapsed heading.</summary>
   IEnumerable<DashboardRow> RowsInOrder(IEnumerable<DashboardRow> rows, bool expandAll)
   {
      foreach(var row in rows)
      {
         yield return row;
         if(row is not DashboardGroup group) continue;
         if(expandAll || group.Expanded)
         {
            foreach(var child in RowsInOrder(group.Children, expandAll)) yield return child;
         }
         else if(ActiveSession?.BelongsTo(group) == true) yield return ActiveSession;
      }
   }
}
