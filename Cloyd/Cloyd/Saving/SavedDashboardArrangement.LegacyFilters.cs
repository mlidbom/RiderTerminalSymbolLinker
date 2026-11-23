using System.Text.Json.Serialization;
using Cloyd.ProjectsDashboard.Filters;

namespace Cloyd.Saving;

sealed partial class SavedDashboardArrangement : IJsonOnDeserialized
{
   IncludedSessions? _toolbarFilters;
   const IncludedSessions LegacyAllStatuses = (IncludedSessions)1024;

   /// <summary>Dashboard files written before the three inclusion switches stored just this preference.</summary>
   [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool OnlyPinned { get; private set; }

   /// <summary>The stored membership bits. Older files omit them. An explicit zero switches off every membership.</summary>
   [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IncludedSessions? RootInclusions { get; private set; }
   /// <summary>The single status selected in older versions of the dashboard.</summary>
   [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public SessionActivityFilter ActivityFilter { get; private set; }
   [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IncludedSessions? RootStatuses { get; private set; }

   /// <summary>Read every property before migrating. When only one old strip narrowed the list, preserve its choices.
   /// When both narrowed it, their selected switches now include matches from either strip.</summary>
   void IJsonOnDeserialized.OnDeserialized()
   {
      if(_toolbarFilters is null)
      {
         var memberships = RootInclusions ?? (OnlyPinned ? IncludedSessions.Pinned : IncludedSessions.AllTabsAndOtherSessions);
         var statuses = RootStatuses ?? StatusFromTheDropdown(ActivityFilter);
         ToolbarFilters = CombineLegacyFilters(memberships, statuses);
         if(!IncludesEveryLegacyStatus(statuses)) MigrateLocalAllMemberships();
      }
      RootInclusions = null;
      RootStatuses = null;
      OnlyPinned = false;
      ActivityFilter = SessionActivityFilter.All;
   }

   static bool IncludesEveryLegacyStatus(IncludedSessions statuses) =>
      statuses == IncludedSessions.None || statuses.HasFlag(LegacyAllStatuses);

   /// <summary>The former All memberships switch still respected the global status restriction.
   /// Keep that group following the toolbar statuses even when its parent includes memberships.</summary>
   void MigrateLocalAllMemberships()
   {
      const IncludedSessions memberships = IncludedSessions.All | IncludedSessions.Pinned | IncludedSessions.AllTabsAndOtherSessions;
      foreach(var (key, choices) in GroupFilters)
         if(choices.LocalSetting(IncludedSessions.All) == true)
            GroupFilters[key] = choices.WithLocalSetting(memberships, false);
   }

   IncludedSessions CombineLegacyFilters(IncludedSessions memberships, IncludedSessions statuses)
   {
      var allStatuses = IncludesEveryLegacyStatus(statuses);
      var allMemberships = memberships == IncludedSessions.None || memberships.HasFlag(IncludedSessions.All);
      if(allStatuses)
      {
         if(memberships == IncludedSessions.AllTabsAndOtherSessions && GroupFilters.Count == 0)
         {
            var rememberedStatuses = statuses & ~LegacyAllStatuses;
            return rememberedStatuses == IncludedSessions.None ? IncludedSessions.None : rememberedStatuses | IncludedSessions.All;
         }
         if(!allMemberships) return memberships;
         var previousChoices = memberships | (statuses & ~LegacyAllStatuses);
         return previousChoices == IncludedSessions.None ? IncludedSessions.None : previousChoices | IncludedSessions.All;
      }
      if(allMemberships || memberships.HasFlag(IncludedSessions.AllTabsAndOtherSessions)) return statuses;
      return memberships | statuses;
   }

   static IncludedSessions StatusFromTheDropdown(SessionActivityFilter status) => status switch
   {
      SessionActivityFilter.All => LegacyAllStatuses,
      SessionActivityFilter.Running => IncludedSessions.Running,
      SessionActivityFilter.ReadyToResume => IncludedSessions.ReadyToResume,
      SessionActivityFilter.Working => IncludedSessions.Working,
      SessionActivityFilter.Waiting => IncludedSessions.Waiting,
      SessionActivityFilter.Idle => IncludedSessions.Idle,
      SessionActivityFilter.Error => IncludedSessions.Error,
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown saved session activity")
   };
}
