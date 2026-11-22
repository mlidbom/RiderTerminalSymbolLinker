namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>The three independently included sets. A session appears when it belongs to any enabled set.</summary>
[Flags]
enum IncludedSessions { None = 0, OpenTabs = 1, Pinned = 2, OutsideTabs = 4, AllTabsAndOtherSessions = OpenTabs | OutsideTabs }
