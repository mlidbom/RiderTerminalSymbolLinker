using Avalonia.Media;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>The status switches above the tree. Running includes every running agent, whatever
/// its activity. The more specific switches allow combinations such as waiting or ready to resume.</summary>
sealed class SessionStatusFilterStrip : DashboardFilterStrip
{
   public SessionStatusFilterStrip() : base(IncludedSessions.AllStatuses)
   {
      Add(IncludedSessions.Running, "Running", Geometry.Parse("M5 2 L19 10 L5 18 Z"));
      Add(IncludedSessions.ReadyToResume, "Ready to resume", Geometry.Parse("M3 4 L5 6 A8 8 0 1 1 3 14 L5 13 A6 6 0 1 0 7 7 L10 10 H3 Z M10 6 H12 V10 L15 12 L14 14 L10 11 Z"));
      Add(IncludedSessions.Working, "Working", Geometry.Parse("M12 1 L3 12 H9 L8 21 L19 8 H12 Z"));
      Add(IncludedSessions.Waiting, "Waiting for you", Geometry.Parse("M4 2 H16 V4 H15 V7 L12 10 L15 13 V16 H16 V18 H4 V16 H5 V13 L8 10 L5 7 V4 H4 Z M7 4 V6 L10 9 L13 6 V4 Z M7 16 H13 V14 L10 11 L7 14 Z"));
      Add(IncludedSessions.Idle, "Idle", Geometry.Parse("M4 3 H8 V17 H4 Z M12 3 H16 V17 H12 Z"));
      Add(IncludedSessions.Error, "Error", Geometry.Parse("M10 1 L20 18 H0 Z M9 7 V12 H11 V7 Z M9 14 V16 H11 V14 Z"));
      AddAll("All statuses");
   }
}
