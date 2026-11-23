using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>The toolbar and every group offer the same inclusion switches. Hover or keyboard navigation
/// reveals every choice. Local overrides stay visible, including switches explicitly turned off.</summary>
sealed class SessionFilterStrip : WrapPanel
{
   DashboardTree? _tree;
   DashboardGroup? Group => DataContext as DashboardGroup;
   bool _keyboardNavigation;
   IPointer? _pressedPointer;

   /// <summary>Leave space for the group name and actions when many switches are visible in a narrow sidebar.</summary>
   public static FuncValueConverter<double, double> MaximumWidthOnHeading { get; } = new(width => Math.Max(24, width - 140));

   static readonly Geometry Pinned = Geometry.Parse("M5 1 H15 V3 H14 V9 L17 12 V14 H11 V19 L10 22 L9 19 V14 H3 V12 L6 9 V3 H5 Z");
   static readonly Geometry OpenTab = Geometry.Parse("M2 3 H18 V17 H2 Z M4 8 V15 H16 V8 Z M4 5 V6 H9 V5 Z");
   static readonly Geometry OutsideTabs = Geometry.Parse("M2 5 H8 L10 7 H18 V17 H2 Z M4 9 V15 H16 V9 Z");

   static readonly Geometry Running = Geometry.Parse("M5 2 L19 10 L5 18 Z");
   static readonly Geometry ReadyToResume = Geometry.Parse("M3 4 L5 6 A8 8 0 1 1 3 14 L5 13 A6 6 0 1 0 7 7 L10 10 H3 Z M10 6 H12 V10 L15 12 L14 14 L10 11 Z");
   static readonly Geometry Working = Geometry.Parse("M12 1 L3 12 H9 L8 21 L19 8 H12 Z");
   static readonly Geometry Waiting = Geometry.Parse("M4 2 H16 V4 H15 V7 L12 10 L15 13 V16 H16 V18 H4 V16 H5 V13 L8 10 L5 7 V4 H4 Z M7 4 V6 L10 9 L13 6 V4 Z M7 16 H13 V14 L10 11 L7 14 Z");
   static readonly Geometry Idle = Geometry.Parse("M4 3 H8 V17 H4 Z M12 3 H16 V17 H12 Z");
   static readonly Geometry Error = Geometry.Parse("M10 1 L20 18 H0 Z M9 7 V12 H11 V7 Z M9 14 V16 H11 V14 Z");

   public SessionFilterStrip()
   {
      Add(IncludedSessions.Pinned, "Pinned", Pinned);
      Add(IncludedSessions.OpenTabs, "Open tabs", OpenTab);
      Add(IncludedSessions.OutsideTabs, "Sessions outside tabs", OutsideTabs);
      Add(IncludedSessions.Running, "Running", Running);
      Add(IncludedSessions.ReadyToResume, "Ready to resume", ReadyToResume);
      Add(IncludedSessions.Working, "Working", Working);
      Add(IncludedSessions.Waiting, "Waiting for you", Waiting);
      Add(IncludedSessions.Idle, "Idle", Idle);
      Add(IncludedSessions.Error, "Error", Error);
      AddSwitch(IncludedSessions.All, "All sessions", new TextBlock { Classes = { "filterAll" }, Text = "A", FontWeight = FontWeight.SemiBold });
      Orientation = Orientation.Horizontal;
      Background = Brushes.Transparent;
      PointerEntered += (_, _) => ShowChoices();
      PointerExited += (_, _) => ShowChoices();
      GotFocus += (_, focus) =>
      {
         _keyboardNavigation = focus.NavigationMethod != NavigationMethod.Pointer;
         ShowChoices();
      };
      LostFocus += (_, _) => Dispatcher.UIThread.Post(ShowChoices, DispatcherPriority.Input);
      AddHandler(PointerPressedEvent, (_, press) =>
      {
         _keyboardNavigation = false;
         if(press.GetCurrentPoint(this).Properties.IsLeftButtonPressed) _pressedPointer = press.Pointer;
      }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
      AddHandler(PointerReleasedEvent, (_, release) => EndPress(release.Pointer), handledEventsToo: true);
      PointerCaptureLost += (_, lost) => EndPress(lost.Pointer);
      AddHandler(KeyDownEvent, (_, key) =>
      {
         if(key.Key != Key.Tab) return;
         _keyboardNavigation = true;
         ShowChoices();
      }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
      Loaded += (_, _) =>
      {
         _tree = (TopLevel.GetTopLevel(this) as ProjectsDashboardWindow)?.Tree;
         if(_tree is not null) _tree.FiltersChanged += Refresh;
         Refresh();
      };
      Unloaded += (_, _) =>
      {
         if(_tree is not null) _tree.FiltersChanged -= Refresh;
         _tree = null;
      };
      DataContextChanged += (_, _) => Refresh();
   }

   /// <summary>A button can take pointer capture during a press. Keep its place in the expanded
   /// strip until it receives the release that performs its click.</summary>
   void EndPress(IPointer pointer)
   {
      if(_pressedPointer != pointer) return;
      _pressedPointer = null;
      Dispatcher.UIThread.Post(ShowChoices, DispatcherPriority.Input);
   }

   void ShowChoices()
   {
      var expanded = _pressedPointer is not null || IsPointerOver || _keyboardNavigation && IsKeyboardFocusWithin;
      foreach(var button in Children.Cast<SessionFilterSwitch>()) button.ShowIfSelectedOrOverridden(expanded);
   }

   void Add(IncludedSessions kind, string name, Geometry icon) =>
      AddSwitch(kind, name, new PathIcon { Data = icon, Width = 13, Height = 13 });

   void AddSwitch(IncludedSessions kind, string name, Control icon)
   {
      var button = new SessionFilterSwitch(kind, name, icon);
      button.Click += (_, _) => _tree?.ToggleInclusion(Group, kind);
      button.UseParentSetting += () => { if(Group is {} group) _tree?.UseParentSetting(group, kind); };
      Children.Add(button);
   }

   void Refresh()
   {
      if(_tree is null) return;
      var filters = _tree.Filters;
      var group = Group;
      var effective = filters.Effective(group);
      foreach(var button in Children.Cast<SessionFilterSwitch>())
         button.Display(filters, group, effective);
      ShowChoices();
   }
}
