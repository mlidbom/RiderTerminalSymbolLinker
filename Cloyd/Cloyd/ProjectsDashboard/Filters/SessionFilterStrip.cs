using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>The same three inclusion switches above the dashboard and beside each group.
/// The check state always shows the effective value, while the tooltip and context menu explain its origin.</summary>
sealed class SessionFilterStrip : StackPanel
{
   static readonly Geometry OpenTabsIcon = Geometry.Parse("M2 3 H18 V17 H2 Z M4 8 V15 H16 V8 Z M4 5 V6 H9 V5 Z");
   static readonly Geometry PinnedIcon = Geometry.Parse("M5 1 H15 V3 H14 V9 L17 12 V14 H11 V19 L10 22 L9 19 V14 H3 V12 L6 9 V3 H5 Z");
   static readonly Geometry OutsideTabsIcon = Geometry.Parse("M2 5 H8 L10 7 H18 V17 H2 Z M4 9 V15 H16 V9 Z");
   DashboardTree? _tree;
   DashboardGroup? Group => DataContext as DashboardGroup;
   readonly Dictionary<IncludedSessions, ToggleButton> _buttons = [];

   public SessionFilterStrip()
   {
      Orientation = Orientation.Horizontal;
      Spacing = 2;
      Add(IncludedSessions.OpenTabs, "Open tabs", OpenTabsIcon);
      Add(IncludedSessions.Pinned, "Pinned", PinnedIcon);
      Add(IncludedSessions.OutsideTabs, "Sessions outside tabs", OutsideTabsIcon);
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

   void Add(IncludedSessions kind, string name, Geometry icon)
   {
      var button = new ToggleButton
      {
         Content = new PathIcon { Data = icon, Width = 13, Height = 13 },
         MinWidth = 22, Height = 23, Padding = new Thickness(4, 2), CornerRadius = new CornerRadius(4)
      };
      AutomationProperties.SetName(button, name);
      button.Click += (_, _) => _tree?.ToggleInclusion(Group, kind);
      button.KeyDown += (_, key) =>
      {
         if(key.Key != Key.Delete || Group is not {} group || _tree is not {} tree || tree.Filters.IsInherited(group, kind)) return;
         tree.UseParentSetting(group, kind);
         key.Handled = true;
      };
      _buttons.Add(kind, button);
      Children.Add(button);
   }

   void Refresh()
   {
      if(_tree is null) return;
      var group = Group;
      var filters = _tree.Filters;
      var included = filters.Effective(group);
      foreach(var (kind, button) in _buttons)
      {
         var inherited = filters.IsInherited(group, kind);
         button.IsChecked = included.HasFlag(kind);
         button.Classes.Set("inherited", inherited);
         var origin = group is null ? "" : inherited ? " · inherited" : " · local override";
         var help = $"{AutomationProperties.GetName(button)} · {(button.IsChecked == true ? "included" : "off")}{origin}";
         if(group is not null && !inherited)
         {
            help += ". Right-click or press Delete to use the parent setting.";
            button.ContextMenu ??= ParentSettingMenu(kind);
         }
         else button.ContextMenu = null;
         ToolTip.SetTip(button, help);
      }
   }

   ContextMenu ParentSettingMenu(IncludedSessions kind)
   {
      var inherit = new MenuItem { Header = "Use parent setting" };
      inherit.Click += (_, _) => { if(Group is {} group) _tree?.UseParentSetting(group, kind); };
      return new ContextMenu { Items = { inherit } };
   }
}
