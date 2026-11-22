namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>Which switches were explicitly set at one heading, and which of those are on.</summary>
sealed record GroupFilterOverride(IncludedSessions Specified = IncludedSessions.None, IncludedSessions Included = IncludedSessions.None)
{
   public static GroupFilterOverride InheritAll { get; } = new();
   public bool Inherits(IncludedSessions kind) => !Specified.HasFlag(kind);
   public IncludedSessions ApplyTo(IncludedSessions inherited) => (inherited & ~Specified) | (Included & Specified);
   public GroupFilterOverride WithLocalSetting(IncludedSessions kind, bool enabled) =>
      new(Specified | kind, enabled ? Included | kind : Included & ~kind);
   public GroupFilterOverride WithParentSetting(IncludedSessions kind) => new(Specified & ~kind, Included & ~kind);
}
