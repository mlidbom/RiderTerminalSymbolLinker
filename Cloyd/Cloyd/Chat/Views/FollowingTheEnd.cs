using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Cloyd.Chat.Views;

/// <summary>
/// A scroll viewer's following of its end, as the chat follows what Claude writes: while Magnus leaves it at the end, it
/// stays there as what it shows grows; once he scrolls up, it stays where he put it, until he scrolls back down to the end.
/// </summary>
/// <remarks>
/// Told by what Magnus does, the wheel, the keys and a press on the scroll bar, not by where the view is: a virtualizing list guesses
/// the length of what it hasn't drawn, and its guesses move the view up and down as it draws more, without Magnus
/// scrolling at all. Seen in the chat of the sample session: 4,019 pixels, then 1,317, then 4,019 again.
/// </remarks>
sealed class FollowingTheEnd
{
   /// <summary>How far from the end the view may be and still be at it, as for the rounding of a layout, in logical pixels.</summary>
   const double NearEnoughTheEnd = 4;

   readonly ScrollViewer _scroller;

   public FollowingTheEnd(ScrollViewer scroller)
   {
      _scroller = scroller;
      scroller.AddHandler(InputElement.PointerWheelChangedEvent, (_, wheel) => { if(wheel.Delta.Y > 0) IsFollowing = false; }, RoutingStrategies.Tunnel, handledEventsToo: true);
      scroller.AddHandler(InputElement.KeyDownEvent, (_, key) => { if(key.Key is Key.Up or Key.PageUp or Key.Home) IsFollowing = false; }, RoutingStrategies.Tunnel, handledEventsToo: true);
      scroller.AddHandler(InputElement.PointerPressedEvent, (_, pressed) => { if(IsOnTheScrollBar(pressed)) IsFollowing = false; }, RoutingStrategies.Tunnel, handledEventsToo: true);
      scroller.ScrollChanged += (_, changed) => KeepToTheEnd(changed);
   }

   /// <summary>Whether the view follows the end: until Magnus scrolls away from it, and again once he scrolls back to it.</summary>
   public bool IsFollowing { get; private set; } = true;

   /// <summary>Scrolls to the end, and follows it from then on.</summary>
   public void ScrollToTheEnd()
   {
      IsFollowing = true;
      _scroller.ScrollToEnd();
   }

   static bool IsOnTheScrollBar(PointerPressedEventArgs pressed) => (pressed.Source as Visual)?.FindAncestorOfType<ScrollBar>(includeSelf: true) is not null;

   bool IsAtTheEnd => _scroller.Offset.Y >= _scroller.Extent.Height - _scroller.Viewport.Height - NearEnoughTheEnd;

   void KeepToTheEnd(ScrollChangedEventArgs changed)
   {
      if(changed.OffsetDelta.Y > 0 && IsAtTheEnd) IsFollowing = true;
      if(IsFollowing && !IsAtTheEnd) _scroller.ScrollToEnd();
   }
}
