namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>The switches offered by the two dashboard filter strips. Each individual switch has
/// a stable bit in saved settings. Each strip's All switch bypasses its own individual choices.</summary>
[Flags]
enum IncludedSessions
{
   None = 0,
   OpenTabs = 1,
   Pinned = 2,
   OutsideTabs = 4,
   AllTabsAndOtherSessions = OpenTabs | OutsideTabs,
   AllMemberships = 512,
   MembershipSwitches = Pinned | OpenTabs | OutsideTabs | AllMemberships,
   Running = 8,
   ReadyToResume = 16,
   Working = 32,
   Waiting = 64,
   Idle = 128,
   Error = 256,
   AllStatuses = 1024,
   StatusSwitches = Running | ReadyToResume | Working | Waiting | Idle | Error | AllStatuses
}
