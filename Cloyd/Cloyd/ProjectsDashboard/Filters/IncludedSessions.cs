namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>Each selected switch includes matching sessions. The bits also identify the switches in saved dashboards.</summary>
[Flags]
enum IncludedSessions
{
   None = 0,
   OpenTabs = 1,
   Pinned = 2,
   OutsideTabs = 4,
   AllTabsAndOtherSessions = OpenTabs | OutsideTabs,
   Running = 8,
   ReadyToResume = 16,
   Working = 32,
   Waiting = 64,
   Idle = 128,
   Error = 256,
   All = 512
}
