using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Cloyd.Sessions;

/// <summary>The insertion line on the edge of a tab or group during a reorder. An adorner keeps
/// the pointer's drop target in place when the line appears.</summary>
static class TabInsertionLine
{
   public static void Show(Control header, Dock edge)
   {
      var vertical = edge is Dock.Left or Dock.Right;
      var line = new Border
      {
         Width = vertical ? 3 : double.NaN, Height = vertical ? double.NaN : 3,
         Margin = vertical ? new Thickness(0, 4) : new Thickness(6, 0), CornerRadius = new CornerRadius(1.5),
         HorizontalAlignment = edge switch { Dock.Left => HorizontalAlignment.Left, Dock.Right => HorizontalAlignment.Right, _ => HorizontalAlignment.Stretch },
         VerticalAlignment = edge switch { Dock.Top => VerticalAlignment.Top, Dock.Bottom => VerticalAlignment.Bottom, _ => VerticalAlignment.Stretch },
         IsHitTestVisible = false
      };
      line.Bind(Border.BackgroundProperty, header.GetResourceObservable("SystemControlHighlightAccentBrush"));
      AdornerLayer.SetAdorner(header, line);
   }

   public static void Hide(Control header) => AdornerLayer.SetAdorner(header, null);
}
