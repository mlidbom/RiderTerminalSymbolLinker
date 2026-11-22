using Cloyd.Hub.RunningSessions;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>Membership switches inherit from the nearest group with an explicit choice. Status switches
/// belong to the whole dashboard. A session must pass both strips, each of which has its own All switch.</summary>
sealed class DashboardSessionFilters(SavedDashboardArrangement saved)
{
   public IncludedSessions Effective(DashboardGroup? group) => MembershipsFor(group) | Statuses;

   IncludedSessions Statuses => DefaultToAll(saved.GlobalStatuses, IncludedSessions.AllStatuses);
   IncludedSessions MembershipsFor(DashboardGroup? group) => DefaultToAll(MembershipChoices(group), IncludedSessions.AllMemberships);
   static IncludedSessions DefaultToAll(IncludedSessions choices, IncludedSessions all) => choices == IncludedSessions.None ? all : choices;

   /// <summary>Inherit the parent's stored choices before applying the empty-strip default at this heading.</summary>
   IncludedSessions MembershipChoices(DashboardGroup? group) => group is null
      ? saved.RootMemberships
      : Override(group).ApplyTo(MembershipChoices(group.Parent)) & IncludedSessions.MembershipSwitches;

   GroupFilterOverride Override(DashboardGroup group) => saved.GroupFilters.GetValueOrDefault(group.Key) ?? GroupFilterOverride.InheritAll;
   public bool IsInherited(DashboardGroup? group, IncludedSessions kind) =>
      group is not null && IsMembershipSwitch(kind) && Override(group).Inherits(kind);
   public bool? LocalSetting(DashboardGroup group, IncludedSessions kind) =>
      IsMembershipSwitch(kind) ? Override(group).LocalSetting(kind) : null;
   public bool AutomaticallyIncludesAll(DashboardGroup? group, IncludedSessions kind) => kind switch
   {
      IncludedSessions.AllMemberships => MembershipChoices(group) == IncludedSessions.None,
      IncludedSessions.AllStatuses => saved.GlobalStatuses == IncludedSessions.None,
      _ => false
   };

   public void Toggle(DashboardGroup? group, IncludedSessions kind)
   {
      if(!IsMembershipSwitch(kind))
      {
         if(group is not null) throw new ArgumentException("Status switches belong to the whole dashboard.", nameof(group));
         saved.GlobalStatuses = ToggleInStrip(Statuses, kind, IncludedSessions.AllStatuses);
      }
      else if(group is null) saved.RootMemberships = ToggleInStrip(MembershipsFor(null), kind, IncludedSessions.AllMemberships);
      else
      {
         var current = MembershipsFor(group);
         var local = Override(group);
         var next = group is DashboardWorktree ? local.Cycle(kind) : local.WithLocalSetting(kind, !current.HasFlag(kind));
         if(kind != IncludedSessions.AllMemberships && current.HasFlag(IncludedSessions.AllMemberships))
            next = next.WithLocalSetting(IncludedSessions.AllMemberships, false);
         SaveOverride(group, next);
      }
   }

   static IncludedSessions ToggleInStrip(IncludedSessions current, IncludedSessions kind, IncludedSessions all) =>
      kind == all ? current ^ all : (current ^ kind) & ~all;

   public void Inherit(DashboardGroup group, IncludedSessions kind)
   {
      if(!IsMembershipSwitch(kind)) throw new ArgumentException("Only membership switches can inherit from a parent group.", nameof(kind));
      SaveOverride(group, Override(group).WithParentSetting(kind));
   }

   void SaveOverride(DashboardGroup group, GroupFilterOverride choices)
   {
      if(choices.Specified == IncludedSessions.None) saved.GroupFilters.Remove(group.Key);
      else saved.GroupFilters[group.Key] = choices;
   }

   public bool Includes(DashboardSession session) =>
      (MembershipsFor(session.Parent) & MembershipsOf(session)) != IncludedSessions.None &&
      (Statuses & StatusesOf(session)) != IncludedSessions.None;

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
      return Statuses.HasFlag(IncludedSessions.AllStatuses) && !group.Sessions.Any() &&
             (memberships.HasFlag(IncludedSessions.AllMemberships) || memberships.HasFlag(IncludedSessions.AllTabsAndOtherSessions));
   }
}
