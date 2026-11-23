using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>A session appears when any switch effective in its group includes it.
/// Each project or worktree inherits individual switches until it overrides them.</summary>
sealed class DashboardSessionFilters(SavedDashboardArrangement saved)
{
   public IncludedSessions Effective(DashboardGroup? group)
   {
      var choices = Choices(group);
      return choices == IncludedSessions.None ? IncludedSessions.All : choices;
   }

   IncludedSessions Choices(DashboardGroup? group) => group is null
      ? saved.ToolbarFilters
      : Override(group).ApplyTo(Choices(group.Parent));

   GroupFilterOverride Override(DashboardGroup group) => saved.GroupFilters.GetValueOrDefault(group.Key) ?? GroupFilterOverride.InheritAll;
   public bool? LocalSetting(DashboardGroup group, IncludedSessions kind) => Override(group).LocalSetting(kind);
   public bool AutomaticallyIncludesAll(DashboardGroup? group) => Choices(group) == IncludedSessions.None;

   public void Toggle(DashboardGroup? group, IncludedSessions kind)
   {
      var current = Effective(group);
      if(group is null)
      {
         saved.ToolbarFilters = kind == IncludedSessions.All ? current ^ kind : (current ^ kind) & ~IncludedSessions.All;
         return;
      }

      var next = Override(group).Cycle(kind);
      if(kind != IncludedSessions.All && current.HasFlag(IncludedSessions.All))
         next = next.WithLocalSetting(IncludedSessions.All, false);
      SaveOverride(group, next);
   }

   public void Inherit(DashboardGroup group, IncludedSessions kind) => SaveOverride(group, Override(group).WithParentSetting(kind));
   public bool HasOverrides => saved.GroupFilters.Count != 0;
   public void ResetOverrides() => saved.GroupFilters.Clear();

   void SaveOverride(DashboardGroup group, GroupFilterOverride choices)
   {
      if(choices.Specified == IncludedSessions.None) saved.GroupFilters.Remove(group.Key);
      else saved.GroupFilters[group.Key] = choices;
   }

   public bool Includes(DashboardSession session) => (Effective(session.Parent) & session.MatchingFilters) != IncludedSessions.None;

   public bool ShowsEmpty(DashboardGroup group)
   {
      var choices = Effective(group);
      return !group.Sessions.Any() && (choices.HasFlag(IncludedSessions.All) || choices.HasFlag(IncludedSessions.AllTabsAndOtherSessions));
   }
}
