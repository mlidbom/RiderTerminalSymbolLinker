using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Cloyd.Sessions;

/// <summary>The edge of a session tab where a dragged tab would land. The marker overlays the header
/// so its appearance never shifts the drop target.</summary>
sealed record TabInsertionPoint(SessionTab Neighbour, bool After)
{
   public void Show()
   {
      var mark = new Border
      {
         Width = 3, Margin = new Thickness(0, 4), CornerRadius = new CornerRadius(1.5),
         HorizontalAlignment = After ? HorizontalAlignment.Right : HorizontalAlignment.Left,
         IsHitTestVisible = false
      };
      mark.Bind(Border.BackgroundProperty, Neighbour.GetResourceObservable("SystemControlHighlightAccentBrush"));
      AdornerLayer.SetAdorner(Neighbour, mark);
   }

   public void Hide() => AdornerLayer.SetAdorner(Neighbour, null);
}
