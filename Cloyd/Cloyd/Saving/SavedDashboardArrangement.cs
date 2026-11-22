namespace Cloyd.Saving;

/// <summary>Magnus's ordering of the dashboard's sibling groups and tabs and the groups he collapsed.
/// Stable keys let a session refresh and a later Cloyd process restore the same arrangement.</summary>
sealed class SavedDashboardArrangement
{
   public List<string> Collapsed { get; init; } = [];
   public List<string> Repositories { get; set; } = [];
   public HashSet<string> PinnedSessions { get; init; } = [];
   public bool OnlyPinned { get; set; }
   public Dictionary<string, List<string>> Order
   {
      get => field;
      init => field = new Dictionary<string, List<string>>(value, StringComparer.OrdinalIgnoreCase);
   } = new(StringComparer.OrdinalIgnoreCase);

   public void SetExpanded(string group, bool expanded)
   {
      Collapsed.RemoveAll(it => StringComparer.OrdinalIgnoreCase.Equals(it, group));
      if(!expanded) Collapsed.Add(group);
   }

   public void RememberOrder(string parent, IEnumerable<string> siblings)
   {
      var current = new Queue<string>(siblings);
      var visible = current.ToHashSet(StringComparer.OrdinalIgnoreCase);
      var ordered = new List<string>();
      if(Order.TryGetValue(parent, out var previous))
         foreach(var key in previous)
            ordered.Add(visible.Contains(key) ? current.Dequeue() : key);
      ordered.AddRange(current);
      Order[parent] = ordered;
   }
}
