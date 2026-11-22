using Avalonia.Controls;

namespace Cloyd.Sessions;

/// <summary>The edge of a project tab where a dragged tab would land.</summary>
sealed record TabInsertionPoint(SessionTab Neighbour, bool After)
{
   public void Show() => TabInsertionLine.Show(Neighbour, After ? Dock.Right : Dock.Left);
   public void Hide() => TabInsertionLine.Hide(Neighbour);
}
