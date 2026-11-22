namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>The values stored for the original single-choice status dropdown.
/// <see cref="Saving.SavedDashboardArrangement"/> migrates them to the dashboard's global status switches.</summary>
enum SessionActivityFilter
{
   All = 0,
   Running = 1,
   ReadyToResume = 2,
   Working = 3,
   Waiting = 4,
   Idle = 5,
   Error = 6
}
