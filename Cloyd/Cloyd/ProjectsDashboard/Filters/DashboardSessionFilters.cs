using Cloyd.Hub.RunningSessions;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>Computes each switch from its nearest explicit setting. A child can include sessions even when
/// every switch is off at its parent. The activity selector further restricts the included sessions.</summary>
sealed class DashboardSessionFilters(SavedDashboardArrangement saved)
{
   public IncludedSessions Effective(DashboardGroup? group) => group is null
      ? saved.RootInclusions ?? (saved.OnlyPinned ? IncludedSessions.Pinned : IncludedSessions.AllTabsAndOtherSessions)
      : Override(group).ApplyTo(Effective(group.Parent));

   GroupFilterOverride Override(DashboardGroup group) => saved.GroupFilters.GetValueOrDefault(group.Key) ?? GroupFilterOverride.InheritAll;
   public bool IsInherited(DashboardGroup? group, IncludedSessions kind) => group is not null && Override(group).Inherits(kind);

   public void Toggle(DashboardGroup? group, IncludedSessions kind)
   {
      var next = Effective(group) ^ kind;
      if(group is null) { saved.RootInclusions = next; return; }
      saved.GroupFilters[group.Key] = Override(group).WithLocalSetting(kind, next.HasFlag(kind));
   }

   public void Inherit(DashboardGroup group, IncludedSessions kind)
   {
      var remaining = Override(group).WithParentSetting(kind);
      if(remaining.Specified == IncludedSessions.None) saved.GroupFilters.Remove(group.Key);
      else saved.GroupFilters[group.Key] = remaining;
   }

   public bool Includes(DashboardSession session)
   {
      var memberships = session.OpenInProjectTab ? IncludedSessions.OpenTabs : IncludedSessions.OutsideTabs;
      if(session.MatchesPinFilter) memberships |= IncludedSessions.Pinned;
      return (Effective(session.Parent) & memberships) != IncludedSessions.None && MatchesActivity(session);
   }

   bool MatchesActivity(DashboardSession session) =>
      saved.ActivityFilter switch
      {
         SessionActivityFilter.All => true,
         SessionActivityFilter.Running => session.Running is not null,
         SessionActivityFilter.ReadyToResume => session.ReadyToResume,
         SessionActivityFilter.Working => session.Running?.IsBusy == true,
         SessionActivityFilter.Waiting => session.IsWaiting,
         SessionActivityFilter.Idle => session.Running is RunningClaudeCode {Status.AsReported: "idle" or "shell"} or RunningCodex {Status.AsReported: "idle"},
         SessionActivityFilter.Error => session.Running is RunningCodex {Status.AsReported: "error"},
         _ => throw new ArgumentOutOfRangeException()
      };

   public bool ShowsEmpty(DashboardGroup group) => saved.ActivityFilter == SessionActivityFilter.All &&
      Effective(group).HasFlag(IncludedSessions.AllTabsAndOtherSessions) && !group.Sessions.Any();
}
