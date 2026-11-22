using Avalonia.Controls;
using Cloyd.Sessions;

namespace Cloyd.ProjectsDashboard;

/// <summary>Where a dragged tab or group would land. The edge of a long expanded group can be outside
/// the visible strip and have no realized row to carry its insertion line.</summary>
sealed record DashboardTabDrop(DashboardRow Neighbour, bool After, Control? Edge)
{
   public void Show() { if(Edge is not null) TabInsertionLine.Show(Edge, After ? Dock.Bottom : Dock.Top); }
   public void Hide() { if(Edge is not null) TabInsertionLine.Hide(Edge); }
}
