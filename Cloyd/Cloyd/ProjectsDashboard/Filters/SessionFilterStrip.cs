using Avalonia.Media;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>The membership switches above the dashboard and beside each project and worktree.</summary>
sealed class SessionFilterStrip : DashboardFilterStrip
{
   static readonly Geometry Pinned = Geometry.Parse("M5 1 H15 V3 H14 V9 L17 12 V14 H11 V19 L10 22 L9 19 V14 H3 V12 L6 9 V3 H5 Z");
   static readonly Geometry OpenTab = Geometry.Parse("M2 3 H18 V17 H2 Z M4 8 V15 H16 V8 Z M4 5 V6 H9 V5 Z");
   static readonly Geometry OutsideTabs = Geometry.Parse("M2 5 H8 L10 7 H18 V17 H2 Z M4 9 V15 H16 V9 Z");

   public SessionFilterStrip() : base(IncludedSessions.AllMemberships)
   {
      Add(IncludedSessions.Pinned, "Pinned", Pinned);
      Add(IncludedSessions.OpenTabs, "Open tabs", OpenTab);
      Add(IncludedSessions.OutsideTabs, "Sessions outside tabs", OutsideTabs);
      AddAll("All session memberships");
   }
}
