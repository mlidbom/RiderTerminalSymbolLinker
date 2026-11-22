using Cloyd.Hub.RunningSessions;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>Membership switches inherit from the nearest group with an explicit choice. Status switches
/// belong to the whole dashboard. A session must pass both strips, each of which has its own All switch.</summary>
sealed class DashboardSessionFilters(SavedDashboardArrangement saved)
{
   public IncludedSessions Effective(DashboardGroup? group) => MembershipsFor(group) | saved.GlobalStatuses;

   IncludedSessions MembershipsFor(DashboardGroup? group) => group is null
      ? saved.RootMemberships
      : Override(group).ApplyTo(MembershipsFor(group.Parent)) & IncludedSessions.MembershipSwitches;

   GroupFilterOverride Override(DashboardGroup group) => saved.GroupFilters.GetValueOrDefault(group.Key) ?? GroupFilterOverride.InheritAll;
   public bool IsInherited(DashboardGroup? group, IncludedSessions kind) =>
      group is not null && IsMembershipSwitch(kind) && Override(group).Inherits(kind);

   public void Toggle(DashboardGroup? group, IncludedSessions kind)
   {
      if(!IsMembershipSwitch(kind))
      {
         if(group is not null) throw new ArgumentException("Status switches belong to the whole dashboard.", nameof(group));
         saved.GlobalStatuses ^= kind;
      }
      else if(group is null) saved.RootMemberships ^= kind;
      else saved.GroupFilters[group.Key] = Override(group).WithLocalSetting(kind, !MembershipsFor(group).HasFlag(kind));
   }

   public void Inherit(DashboardGroup group, IncludedSessions kind)
   {
      if(!IsMembershipSwitch(kind)) throw new ArgumentException("Only membership switches can inherit from a parent group.", nameof(kind));
      var remaining = Override(group).WithParentSetting(kind);
      if(remaining.Specified == IncludedSessions.None) saved.GroupFilters.Remove(group.Key);
      else saved.GroupFilters[group.Key] = remaining;
   }

   public bool Includes(DashboardSession session) =>
      (MembershipsFor(session.Parent) & MembershipsOf(session)) != IncludedSessions.None &&
      (saved.GlobalStatuses & StatusesOf(session)) != IncludedSessions.None;

   static bool IsMembershipSwitch(IncludedSessions kind) => (kind & IncludedSessions.MembershipSwitches) != IncludedSessions.None;

   static IncludedSessions MembershipsOf(DashboardSession session)
   {
      var memberships = IncludedSessions.AllMemberships | (session.OpenInProjectTab ? IncludedSessions.OpenTabs : IncludedSessions.OutsideTabs);
      return session.MatchesPinFilter ? memberships | IncludedSessions.Pinned : memberships;
   }

   /// <summary>Every running agent matches Running, including an agent reporting an unfamiliar activity.
   /// Its more specific switch can independently include it. All statuses also includes a bare shell.</summary>
   static IncludedSessions StatusesOf(DashboardSession session)
   {
      const IncludedSessions all = IncludedSessions.AllStatuses;
      if(session.Running is not {} running) return session.ReadyToResume ? all | IncludedSessions.ReadyToResume : all;
      var activity = running switch
      {
         { IsBusy: true } => IncludedSessions.Working,
         _ when session.IsWaiting => IncludedSessions.Waiting,
         RunningClaudeCode {Status.AsReported: "idle" or "shell"} or RunningCodex {Status.AsReported: "idle"} => IncludedSessions.Idle,
         RunningCodex {Status.AsReported: "error"} => IncludedSessions.Error,
         _ => IncludedSessions.None
      };
      return all | IncludedSessions.Running | activity;
   }

   public bool ShowsEmpty(DashboardGroup group)
   {
      var memberships = MembershipsFor(group);
      return saved.GlobalStatuses.HasFlag(IncludedSessions.AllStatuses) && !group.Sessions.Any() &&
             (memberships.HasFlag(IncludedSessions.AllMemberships) || memberships.HasFlag(IncludedSessions.AllTabsAndOtherSessions));
   }
}
