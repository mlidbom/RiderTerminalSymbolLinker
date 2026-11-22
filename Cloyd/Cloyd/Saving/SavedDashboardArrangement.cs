using Cloyd.ProjectsDashboard.Filters;
using System.Text.Json.Serialization;

namespace Cloyd.Saving;

/// <summary>Magnus's ordering of the dashboard's sibling groups and tabs and the groups he collapsed.
/// Stable keys let a session refresh and a later Cloyd process restore the same arrangement.</summary>
sealed class SavedDashboardArrangement : IJsonOnDeserialized
{
   public List<string> Collapsed { get; init; } = [];
   public List<string> Repositories { get; set; } = [];
   public HashSet<string> PinnedSessions { get; init; } = [];
   /// <summary>Dashboard files written before the three inclusion switches stored just this preference.</summary>
   [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool OnlyPinned { get; private set; }

   /// <summary>The stored membership bits. Older files omit them. An explicit zero switches off every membership.</summary>
   [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IncludedSessions? RootInclusions { get; private set; }
   public Dictionary<string, GroupFilterOverride> GroupFilters
   {
      get => field;
      init => field = new Dictionary<string, GroupFilterOverride>(value, StringComparer.OrdinalIgnoreCase);
   } = new(StringComparer.OrdinalIgnoreCase);
   /// <summary>The single status selected in older versions of the dashboard.</summary>
   [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public SessionActivityFilter ActivityFilter { get; private set; }
   [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IncludedSessions? RootStatuses { get; private set; }

   /// <summary>The root membership choices which projects and worktrees inherit.</summary>
   [JsonIgnore] public IncludedSessions RootMemberships
   {
      get => RootInclusions ?? IncludedSessions.AllTabsAndOtherSessions;
      set => RootInclusions = value & IncludedSessions.MembershipSwitches;
   }

   /// <summary>The single set of status choices applied throughout Projects Dashboard.</summary>
   [JsonIgnore] public IncludedSessions GlobalStatuses
   {
      get => RootStatuses ?? IncludedSessions.AllStatuses;
      set => RootStatuses = value & IncludedSessions.StatusSwitches;
   }

   /// <summary>Read all JSON properties first, so the order of old and new settings cannot change which ones win.</summary>
   void IJsonOnDeserialized.OnDeserialized()
   {
      RootMemberships = RootInclusions ?? (OnlyPinned ? IncludedSessions.Pinned : IncludedSessions.AllTabsAndOtherSessions);
      GlobalStatuses = RootStatuses ?? StatusFromTheDropdown(ActivityFilter);
      OnlyPinned = false;
      ActivityFilter = SessionActivityFilter.All;
   }

   static IncludedSessions StatusFromTheDropdown(SessionActivityFilter status) => status switch
   {
      SessionActivityFilter.All => IncludedSessions.AllStatuses,
      SessionActivityFilter.Running => IncludedSessions.Running,
      SessionActivityFilter.ReadyToResume => IncludedSessions.ReadyToResume,
      SessionActivityFilter.Working => IncludedSessions.Working,
      SessionActivityFilter.Waiting => IncludedSessions.Waiting,
      SessionActivityFilter.Idle => IncludedSessions.Idle,
      SessionActivityFilter.Error => IncludedSessions.Error,
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown saved session activity")
   };
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
