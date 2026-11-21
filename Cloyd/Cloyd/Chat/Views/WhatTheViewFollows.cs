using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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

   /// <summary>Scrolls to what the view follows in place of the end: null while it follows none.</summary>
   Action? _scrollToWhatItFollows;

   public WhatTheViewFollows(ScrollViewer scroller)
   {
      _scroller = scroller;
      scroller.AddHandler(InputElement.PointerWheelChangedEvent, (_, wheel) => MagnusScrolled(up: wheel.Delta.Y > 0), RoutingStrategies.Tunnel, handledEventsToo: true);
      scroller.AddHandler(InputElement.KeyDownEvent, (_, key) => { if(key.Key is Key.Up or Key.PageUp or Key.Home or Key.Down or Key.PageDown or Key.End) MagnusScrolled(up: key.Key is Key.Up or Key.PageUp or Key.Home); }, RoutingStrategies.Tunnel, handledEventsToo: true);
      scroller.AddHandler(InputElement.PointerPressedEvent, (_, pressed) => { if(IsOnTheScrollBar(pressed)) MagnusScrolled(up: true); }, RoutingStrategies.Tunnel, handledEventsToo: true);
      scroller.ScrollChanged += (_, changed) => KeepInView(changed);
   }

   /// <summary>Whether the view follows the end: until Magnus scrolls away from it, or the view follows something in place of it, and again once he scrolls back to it.</summary>
   public bool IsFollowingTheEnd { get; private set; } = true;

   /// <summary>Scrolls to the end, and follows it from then on.</summary>
   public void ScrollToTheEnd()
   {
      _scrollToWhatItFollows = null;
      IsFollowingTheEnd = true;
      _scroller.ScrollToEnd();
   }

   /// <summary>
   /// Follows what <paramref name="scrollToIt"/> scrolls to, in place of the end, as a match a search shows: scrolls to it
   /// now, and again each time what is drawn grows, until Magnus scrolls.
   /// </summary>
   public void FollowInPlaceOfTheEnd(Action scrollToIt)
   {
      IsFollowingTheEnd = false;
      _scrollToWhatItFollows = scrollToIt;
      scrollToIt();
   }

   /// <summary>Stops following what it follows in place of the end, as when the search that showed it ends.</summary>
   public void StopFollowingInPlaceOfTheEnd() => _scrollToWhatItFollows = null;

   static bool IsOnTheScrollBar(PointerPressedEventArgs pressed) => (pressed.Source as Visual)?.FindAncestorOfType<ScrollBar>(includeSelf: true) is not null;

   bool IsAtTheEnd => _scroller.Offset.Y >= _scroller.Extent.Height - _scroller.Viewport.Height - NearEnoughTheEnd;

   /// <summary>Magnus scrolls the view, <paramref name="up"/> or down: it no longer follows what it followed in place of the end, nor the end, as he scrolls up.</summary>
   void MagnusScrolled(bool up)
   {
      _scrollToWhatItFollows = null;
      if(up) IsFollowingTheEnd = false;
   }

   void KeepInView(ScrollChangedEventArgs changed)
   {
      if(_scrollToWhatItFollows is {} scrollToIt)
      {
         if(changed.ExtentDelta.Y != 0) scrollToIt();
         return;
      }

      if(changed.OffsetDelta.Y > 0 && IsAtTheEnd) IsFollowingTheEnd = true;
      if(IsFollowingTheEnd && !IsAtTheEnd) _scroller.ScrollToEnd();
   }
}
