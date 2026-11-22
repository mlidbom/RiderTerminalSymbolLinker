using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard;

/// <summary>The ordering and expansion Magnus chose for the groups and tabs of a
/// <see cref="DashboardHierarchy"/>. Moving a row changes its position among its siblings.</summary>
sealed class DashboardArrangement
{
   const string RootKey = "projects";
   public SavedDashboardArrangement Saved { get; set; } = new();

   /// <summary>Restore the saved order of known rows, following them with newly discovered siblings.
   /// Projects without a saved position are alphabetical, with outside-host sessions last.</summary>
   public void Restore(DashboardHierarchy hierarchy)
   {
      var alphabetical = hierarchy.Projects.OrderBy(it => it.Project is null).ThenBy(it => it.Name, StringComparer.OrdinalIgnoreCase).ToArray();
      hierarchy.Roots.Clear();
      hierarchy.Roots.AddRange(alphabetical);
      OrderSiblings(RootKey, hierarchy.Roots);
      foreach(var group in hierarchy.Groups)
      {
         group.Expanded = !Saved.Collapsed.Contains(group.Key, StringComparer.OrdinalIgnoreCase);
         OrderSiblings(group.Key, group.Children);
         group.Summarize();
      }
   }

   public void Toggle(DashboardGroup group)
   {
      group.Expanded = !group.Expanded;
      Saved.SetExpanded(group.Key, group.Expanded);
      group.Summarize();
   }

   public void Move(DashboardHierarchy hierarchy, DashboardRow row, DashboardRow neighbour, bool after)
   {
      if(row == neighbour || row.Parent != neighbour.Parent || row.GetType() != neighbour.GetType()) return;
      var siblings = row.Parent?.Children ?? hierarchy.Roots;
      if(!siblings.Contains(neighbour) || !siblings.Remove(row)) return;
      siblings.Insert(siblings.IndexOf(neighbour) + (after ? 1 : 0), row);
      Saved.RememberOrder(row.Parent?.Key ?? RootKey, siblings.Select(it => it.Key));
   }

   void OrderSiblings(string parent, List<DashboardRow> rows)
   {
      if(!Saved.Order.TryGetValue(parent, out var order)) return;
      var sorted = rows.OrderBy(Position).ToArray();
      rows.Clear();
      rows.AddRange(sorted);

      int Position(DashboardRow row)
      {
         var index = order.FindIndex(it => StringComparer.OrdinalIgnoreCase.Equals(it, row.Key));
         return index < 0 ? int.MaxValue : index;
      }
   }
}
