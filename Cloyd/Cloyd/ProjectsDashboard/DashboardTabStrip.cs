using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cloyd.ProjectsDashboard;

/// <summary>One vertical tab strip for every project and worktree. Keyboard focus can move through
/// the headings independently of the active session. A pointer release activates a row or finishes a reorder.</summary>
sealed class DashboardTabStrip : ListBox
{
   readonly DispatcherTimer _scrollWhileDragging = new() { Interval = TimeSpan.FromMilliseconds(55) };
   (DashboardRow Row, IPointer Pointer, Point Start)? _press;
   bool _dragging;
   Point _at;
   DashboardTabDrop? _drop;
   static readonly Cursor MoveCursor = new(StandardCursorType.DragMove);
   DashboardTree Tree => (DashboardTree)DataContext!;
   public event Action<DashboardSession>? TabChosen;

   public DashboardTabStrip()
   {
      AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
      AddHandler(PointerMovedEvent, Moved, RoutingStrategies.Tunnel);
      AddHandler(PointerReleasedEvent, Released, RoutingStrategies.Tunnel);
      Tapped += (_, tap) =>
      {
         if(tap.Pointer.Type != PointerType.Mouse && tap.Source is Visual hit &&
            hit.FindAncestorOfType<Button>(includeSelf: true) is null &&
            hit.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is DashboardRow row) Activate(row);
      };
      AddHandler(KeyDownEvent, Navigate, RoutingStrategies.Tunnel);
      _scrollWhileDragging.Tick += (_, _) => ScrollWhileDragging();
      Unloaded += (_, _) => EndGesture();
   }

   protected override Type StyleKeyOverride => typeof(ListBox);

   void Pressed(object? sender, PointerPressedEventArgs e)
   {
      if(_press is not null || e.Pointer.Type != PointerType.Mouse || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed ||
         e.Source is not Visual hit || hit.FindAncestorOfType<Button>(includeSelf: true) is not null ||
         hit.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is not DashboardRow row) return;
      e.Handled = true;
      _at = e.GetPosition(this);
      e.Pointer.Capture(this);
      if(ReferenceEquals(e.Pointer.Captured, this)) _press = (row, e.Pointer, _at);
   }

   void Moved(object? sender, PointerEventArgs e)
   {
      if(_press is not {} press || e.Pointer != press.Pointer) return;
      if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { EndGesture(); return; }
      _at = e.GetPosition(this);
      if(!_dragging)
      {
         var delta = _at - press.Start;
         var threshold = Application.Current!.PlatformSettings!.GetTapSize(e.Pointer.Type);
         if(Math.Abs(delta.X) < threshold.Width && Math.Abs(delta.Y) < threshold.Height) return;
         _dragging = true;
         Cursor = MoveCursor;
         _scrollWhileDragging.Start();
      }
      Mark(DropAt(press.Row, _at));
      e.Handled = true;
   }

   void Released(object? sender, PointerReleasedEventArgs e)
   {
      if(_press is not {} press || e.Pointer != press.Pointer || e.InitialPressMouseButton != MouseButton.Left) return;
      var at = e.GetPosition(this);
      var drag = _dragging;
      var drop = drag ? DropAt(press.Row, at) : null;
      EndGesture();
      e.Handled = true;
      if(drop is not null) Tree.Move(press.Row, drop.Neighbour, drop.After);
      else if(!drag && ReferenceEquals(RowAt(at)?.DataContext, press.Row)) Activate(press.Row);
   }

   void Activate(DashboardRow row)
   {
      if(row is DashboardGroup group) Tree.Toggle(group);
      else if(row is DashboardSession session) TabChosen?.Invoke(session);
   }

   void Navigate(object? sender, KeyEventArgs key)
   {
      if(SelectedItem is not DashboardRow row || key.KeyModifiers != KeyModifiers.None ||
         key.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null) return;
      switch(key.Key)
      {
         case Key.Space or Key.Enter: Activate(row); break;
         case Key.Left when row is DashboardGroup { Expanded: true } group: Tree.Toggle(group); break;
         case Key.Left when row.Parent is {} parent: SelectedItem = parent; break;
         case Key.Right when row is DashboardGroup { Expanded: false } group: Tree.Toggle(group); break;
         case Key.Right when row is DashboardGroup expanded && Tree.VisibleRows.FirstOrDefault(it => it.Parent == expanded) is {} first: SelectedItem = first; break;
         default: return;
      }
      key.Handled = true;
   }

   public bool CancelDrag()
   {
      if(!_dragging) return false;
      EndGesture();
      return true;
   }

   protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
   {
      base.OnPointerCaptureLost(e);
      if(_press?.Pointer == e.Pointer) EndGesture();
   }

   void EndGesture()
   {
      var pointer = _press?.Pointer;
      _press = null;
      _dragging = false;
      Mark(null);
      _scrollWhileDragging.Stop();
      ClearValue(CursorProperty);
      if(pointer is not null && ReferenceEquals(pointer.Captured, this)) pointer.Capture(null);
   }

   ListBoxItem? RowAt(Point point)
   {
      if(!new Rect(Bounds.Size).Contains(point)) return null;
      return GetRealizedContainers().OfType<ListBoxItem>().FirstOrDefault(it =>
         it.TranslatePoint(default, this) is {} top && point.Y >= top.Y && point.Y < top.Y + it.Bounds.Height);
   }

   /// <summary>Dragging a group across its neighbour's expanded children moves the whole group past that neighbour.</summary>
   DashboardTabDrop? DropAt(DashboardRow dragged, Point point)
   {
      if(RowAt(point) is not {} item || item.DataContext is not DashboardRow hovered) return null;
      var neighbour = hovered;
      while(neighbour.Parent != dragged.Parent && neighbour.Parent is {} parent) neighbour = parent;
      if(neighbour == dragged || neighbour.Parent != dragged.Parent || neighbour.GetType() != dragged.GetType()) return null;
      var after = hovered != neighbour || point.Y >= item.TranslatePoint(default, this)!.Value.Y + item.Bounds.Height / 2;
      var edge = neighbour;
      if(after && neighbour is DashboardGroup group)
         edge = Tree.VisibleRows.LastOrDefault(it => it.BelongsTo(group)) ?? neighbour;
      return new DashboardTabDrop(neighbour, after, ContainerFromItem(edge));
   }

   void Mark(DashboardTabDrop? drop)
   {
      if(_drop == drop) return;
      _drop?.Hide();
      _drop = drop;
      _drop?.Show();
   }

   void ScrollWhileDragging()
   {
      if(_press is not {} press || !new Rect(Bounds.Size).Contains(_at)) return;
      var step = _at.Y < 24 ? -16 : _at.Y > Bounds.Height - 24 ? 16 : 0;
      if(step == 0 || this.FindDescendantOfType<ScrollViewer>() is not {} scroll) return;
      scroll.Offset = scroll.Offset.WithY(scroll.Offset.Y + step);
      Mark(DropAt(press.Row, _at));
   }
}
