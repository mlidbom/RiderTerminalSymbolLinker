using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Cloyd.Sessions;

/// <summary>A project's tabs in display order. Dragging a header previews an insertion point and dropping
/// moves the existing tab, including its session host attachment and chat draft.</summary>
sealed class SessionTabStrip : TabControl
{
   static readonly Cursor MoveCursor = new(StandardCursorType.DragMove);
   readonly ObservableCollection<SessionTab> _order = [];
   (SessionTab Tab, IPointer Pointer, Point Start)? _press;
   bool _dragging;
   TabInsertionPoint? _insertion;

   public SessionTabStrip()
   {
      ItemsSource = _order;
      AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Bubble, handledEventsToo: true);
      AddHandler(PointerMovedEvent, Moved, RoutingStrategies.Tunnel, handledEventsToo: true);
      AddHandler(PointerReleasedEvent, Released, RoutingStrategies.Tunnel, handledEventsToo: true);
      AddHandler(KeyDownEvent, (_, key) =>
      {
         if(key.Key != Key.Escape || !_dragging) return;
         EndGesture();
         key.Handled = true;
      }, RoutingStrategies.Tunnel, handledEventsToo: true);
   }

   protected override Type StyleKeyOverride => typeof(TabControl);

   public IReadOnlyList<SessionTab> InOrder => _order.ToArray();
   public void Add(SessionTab tab) => _order.Add(tab);

   public void Remove(SessionTab tab)
   {
      if(_press?.Tab == tab) EndGesture();
      if(_insertion?.Neighbour == tab) ShowInsertion(null);
      _order.Remove(tab);
   }

   void Pressed(object? sender, PointerPressedEventArgs pressed)
   {
      if(_press is not null || _order.Count < 2 || pressed.Pointer.Type != PointerType.Mouse ||
         pressed.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;
      if(pressed.Source is not Visual hit || hit.FindAncestorOfType<Button>(includeSelf: true) is not null ||
         hit.FindAncestorOfType<SessionTab>(includeSelf: true) is not {} tab || !_order.Contains(tab)) return;

      // TabItem has already handled selection. Capture on the strip, which stays in place when its tabs move.
      pressed.Pointer.Capture(this);
      if(ReferenceEquals(pressed.Pointer.Captured, this)) _press = (tab, pressed.Pointer, pressed.GetPosition(this));
   }

   void Moved(object? sender, PointerEventArgs moved)
   {
      if(_press is not {} press || moved.Pointer != press.Pointer) return;
      var current = moved.GetCurrentPoint(this);
      if(!current.Properties.IsLeftButtonPressed || !ReferenceEquals(press.Pointer.Captured, this))
      {
         EndGesture();
         return;
      }
      var point = current.Position;
      if(!_dragging)
      {
         var distance = point - press.Start;
         var threshold = Application.Current!.PlatformSettings!.GetTapSize(press.Pointer.Type);
         if(Math.Abs(distance.X) < threshold.Width && Math.Abs(distance.Y) < threshold.Height) return;
         _dragging = true;
         Cursor = MoveCursor;
      }
      ShowInsertion(InsertionAt(point));
      moved.Handled = true;
   }

   void Released(object? sender, PointerReleasedEventArgs released)
   {
      if(_press is not {} press || released.Pointer != press.Pointer || released.InitialPressMouseButton != MouseButton.Left) return;
      var drop = _dragging && ReferenceEquals(press.Pointer.Captured, this) ? InsertionAt(released.GetPosition(this)) : null;
      if(_dragging) released.Handled = true;
      EndGesture();
      if(drop is null) return;

      var from = _order.IndexOf(press.Tab);
      var to = _order.IndexOf(drop.Neighbour) + (drop.After ? 1 : 0);
      if(to > from) to--;
      if(to == from) return;
      var selected = SelectedItem;
      _order.Move(from, to);
      SelectedItem = selected;
   }

   /// <summary>Use the actual header rectangles, including the row the pointer is over when the strip wraps.
   /// Empty space after the last header in a row is also a drop target.</summary>
   TabInsertionPoint? InsertionAt(Point point)
   {
      if(ItemsPanelRoot is not {} panel || this.TranslatePoint(point, panel) is not {} local ||
         local.X < 0 || point.X >= Bounds.Width || local.Y < 0 || local.Y >= panel.Bounds.Height) return null;
      SessionTab? lastInRow = null;
      foreach(var tab in _order)
      {
         var bounds = tab.Bounds;
         if(local.Y < bounds.Y || local.Y >= bounds.Bottom) continue;
         if(local.X < bounds.Center.X) return new(tab, After: false);
         lastInRow = tab;
      }
      return lastInRow is null ? null : new(lastInRow, After: true);
   }

   void ShowInsertion(TabInsertionPoint? insertion)
   {
      if(_insertion == insertion) return;
      _insertion?.Hide();
      _insertion = insertion;
      _insertion?.Show();
   }

   void EndGesture()
   {
      var pointer = _press?.Pointer;
      _press = null;
      if(_dragging) ClearValue(CursorProperty);
      _dragging = false;
      ShowInsertion(null);
      if(pointer is not null && ReferenceEquals(pointer.Captured, this)) pointer.Capture(null);
   }

   protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
   {
      base.OnPointerCaptureLost(e);
      if(_press?.Pointer == e.Pointer) EndGesture();
   }
}
