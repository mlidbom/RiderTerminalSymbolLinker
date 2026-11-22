namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>Which switches were explicitly set at one heading, and which of those are on.</summary>
sealed record GroupFilterOverride(IncludedSessions Specified = IncludedSessions.None, IncludedSessions Included = IncludedSessions.None)
{
   public static GroupFilterOverride InheritAll { get; } = new();
   public bool Inherits(IncludedSessions kind) => !Specified.HasFlag(kind);
   /// <summary>On or Off when explicitly set at this group. Null means Unset, which follows the parent.</summary>
   public bool? LocalSetting(IncludedSessions kind) => Inherits(kind) ? null : Included.HasFlag(kind);
   public IncludedSessions ApplyTo(IncludedSessions inherited) => (inherited & ~Specified) | (Included & Specified);
   public GroupFilterOverride WithLocalSetting(IncludedSessions kind, bool enabled) =>
      new(Specified | kind, enabled ? Included | kind : Included & ~kind);
   public GroupFilterOverride WithParentSetting(IncludedSessions kind) => new(Specified & ~kind, Included & ~kind);

   /// <summary>A worktree cycles from its parent's setting to explicit On, then Off, then back to its parent.</summary>
   public GroupFilterOverride Cycle(IncludedSessions kind) => LocalSetting(kind) switch
   {
      null => WithLocalSetting(kind, true),
      true => WithLocalSetting(kind, false),
      false => WithParentSetting(kind)
   };
}
