using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Cloyd.Chat.Views;

/// <summary>
/// What a scroll viewer keeps in view as what it shows grows, as the chat's does: the end, as Claude writes, while Magnus
/// leaves it there; or what the chat shows in place of it, as a match a search found, as what is drawn around it is laid
/// out. Once Magnus scrolls away, the view stays where he put it, until he scrolls back down to the end.
/// </summary>
/// <remarks>
/// Whether Magnus scrolls is told by what he does, the wheel, the keys and a press on the scroll bar, not by where the
/// view is: what is drawn grows without him, as the conversation goes on and as Claude's markdown is laid out after it
/// is drawn, and leaves the view above the end, or away from what it showed.
/// </remarks>
sealed class WhatTheViewFollows
{
   /// <summary>How far from the end the view may be and still be at it, as for the rounding of a layout, in logical pixels.</summary>
   const double NearEnoughTheEnd = 4;

   readonly ScrollViewer _scroller;
   ChatScrollMode _mode = ChatScrollMode.FollowingTheEnd;

   /// <summary>What the view follows in place of the end, and where in it, in its own coordinates: null while it follows none.</summary>
   (Visual Visual, Rect Bounds)? _followedInPlaceOfTheEnd;

   public WhatTheViewFollows(ScrollViewer scroller)
   {
      _scroller = scroller;
      scroller.AddHandler(InputElement.PointerWheelChangedEvent, (_, wheel) => MagnusScrolled(up: wheel.Delta.Y > 0), RoutingStrategies.Tunnel, handledEventsToo: true);
      scroller.AddHandler(InputElement.KeyDownEvent, (_, key) => { if(key.Key is Key.Up or Key.PageUp or Key.Home or Key.Down or Key.PageDown or Key.End) MagnusScrolled(up: key.Key is Key.Up or Key.PageUp or Key.Home); }, RoutingStrategies.Tunnel, handledEventsToo: true);
      scroller.AddHandler(InputElement.PointerPressedEvent, (_, pressed) =>
      {
         if(IsOnTheScrollBar(pressed)) BeginManualScroll(ChatScrollMode.MayResumeAtTheEnd);
      }, RoutingStrategies.Tunnel, handledEventsToo: true);
      scroller.AddHandler(Thumb.DragStartedEvent, (_, drag) =>
      {
         if(IsOnTheScrollBar(drag)) BeginManualScroll(ChatScrollMode.DraggingTheScrollBar);
      }, handledEventsToo: true);
      scroller.AddHandler(Thumb.DragCompletedEvent, (_, drag) =>
      {
         if(IsOnTheScrollBar(drag)) _mode = ChatScrollMode.ReadingInPlace; // Also raised when the thumb loses capture.
      }, handledEventsToo: true);
      scroller.AddHandler(InputElement.PointerReleasedEvent, (_, released) =>
      {
         if(released.InitialPressMouseButton == MouseButton.Left && IsOnTheScrollBar(released))
            _mode = IsAtTheEnd ? ChatScrollMode.FollowingTheEnd : ChatScrollMode.ReadingInPlace;
      }, RoutingStrategies.Bubble, handledEventsToo: true);
      scroller.ScrollChanged += (_, changed) => KeepInView(changed);
   }

   /// <summary>Whether the view follows the end: until Magnus scrolls away from it, or the view follows something in place of it, and again once he scrolls back to it.</summary>
   public bool IsFollowingTheEnd => _mode == ChatScrollMode.FollowingTheEnd;

   /// <summary>Scrolls to the end, and follows it from then on.</summary>
   public void ScrollToTheEnd()
   {
      _followedInPlaceOfTheEnd = null;
      if(_mode == ChatScrollMode.DraggingTheScrollBar) return;
      _mode = ChatScrollMode.FollowingTheEnd;
      _scroller.ScrollToEnd();
   }

   /// <summary>
   /// Follows <paramref name="bounds"/> of <paramref name="visual"/>, in its own coordinates, in place of the end, as a match
   /// a search shows: keeps it in view now, and again each time what is drawn grows, until Magnus scrolls.
   /// </summary>
   /// <remarks>
   /// Scrolls the scroll viewer itself: a match's text block deep in Claude's markdown asked to be brought into view, and
   /// the request never reached the chat's scroll viewer (2026-11-21, in one of Magnus's conversations).
   /// </remarks>
   public void FollowInPlaceOfTheEnd(Visual visual, Rect bounds)
   {
      if(_mode == ChatScrollMode.DraggingTheScrollBar) return;
      _mode = ChatScrollMode.ReadingInPlace;
      _followedInPlaceOfTheEnd = (visual, bounds);
      KeepInView(visual, bounds);
   }

   /// <summary>Stops following what it follows in place of the end, as when the search that showed it ends.</summary>
   public void StopFollowingInPlaceOfTheEnd() => _followedInPlaceOfTheEnd = null;

   bool IsOnTheScrollBar(RoutedEventArgs input) =>
      (input.Source as Visual)?.FindAncestorOfType<ScrollBar>(includeSelf: true) is { Orientation: Orientation.Vertical } bar
      && bar.TemplatedParent == _scroller;

   bool IsAtTheEnd => _scroller.Offset.Y >= _scroller.Extent.Height - _scroller.Viewport.Height - NearEnoughTheEnd;

   /// <summary>Keep the clicked heading at its viewport position even when layout anchors a later transcript item.</summary>
   public void KeepHeaderInPlace(Control header, Action toggle)
   {
      BeginManualScroll(ChatScrollMode.ReadingInPlace);
      var before = HeaderTop();
      toggle();
      _scroller.UpdateLayout();
      _scroller.Offset = _scroller.Offset.WithY(_scroller.Offset.Y + HeaderTop() - before);

      double HeaderTop() => header.TranslatePoint(default, _scroller)!.Value.Y;
   }

   void BeginManualScroll(ChatScrollMode mode)
   {
      _followedInPlaceOfTheEnd = null;
      _mode = mode;
   }

   /// <summary>Magnus scrolls the view, <paramref name="up"/> or down: it no longer follows what it followed in place of the end, nor the end, as he scrolls up.</summary>
   void MagnusScrolled(bool up)
   {
      if(_mode == ChatScrollMode.DraggingTheScrollBar) return;
      BeginManualScroll(up ? ChatScrollMode.ReadingInPlace : IsAtTheEnd ? ChatScrollMode.FollowingTheEnd : ChatScrollMode.MayResumeAtTheEnd);
   }

   void KeepInView(ScrollChangedEventArgs changed)
   {
      if(_mode == ChatScrollMode.DraggingTheScrollBar) return;
      if(_followedInPlaceOfTheEnd is var (visual, bounds))
      {
         if(changed.ExtentDelta.Y != 0) KeepInView(visual, bounds);
         return;
      }

      if(_mode == ChatScrollMode.MayResumeAtTheEnd && changed.OffsetDelta.Y > 0 && IsAtTheEnd) _mode = ChatScrollMode.FollowingTheEnd;
      if(IsFollowingTheEnd && !IsAtTheEnd) _scroller.ScrollToEnd();
   }

   /// <summary>
   /// Scrolls <paramref name="bounds"/> of <paramref name="visual"/> into the middle of the view, as a browser does a match it
   /// finds, unless it is in view already; the top of it, when it is taller than the view.
   /// </summary>
   void KeepInView(Visual visual, Rect bounds)
   {
      if(visual.TranslatePoint(bounds.TopLeft, _scroller) is not { Y: var top }) return;
      if(top >= 0 && top + bounds.Height <= _scroller.Viewport.Height) return;
      _scroller.Offset = _scroller.Offset.WithY(Math.Max(0, _scroller.Offset.Y + top - Math.Max(0, (_scroller.Viewport.Height - bounds.Height) / 2)));
   }
}
