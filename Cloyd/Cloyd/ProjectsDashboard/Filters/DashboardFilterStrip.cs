using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>A row of dashboard switches. All temporarily bypasses the other switches in this
/// strip while their check marks retain the choices that take effect when All is released.</summary>
abstract class DashboardFilterStrip : StackPanel
{
   readonly IncludedSessions _all;
   DashboardTree? _tree;
   DashboardGroup? Group => DataContext as DashboardGroup;
   readonly Dictionary<IncludedSessions, ToggleButton> _buttons = [];

   protected DashboardFilterStrip(IncludedSessions all)
   {
      _all = all;
      Orientation = Orientation.Horizontal;
      Spacing = 2;
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

   protected void Add(IncludedSessions kind, string name, Geometry icon) =>
      AddButton(kind, name, new PathIcon { Data = icon, Width = 13, Height = 13 });

   protected void AddAll(string name) => AddButton(_all, name, new TextBlock { Classes = { "filterAll" }, Text = "A", FontWeight = FontWeight.SemiBold });

   void AddButton(IncludedSessions kind, string name, Control content)
   {
      var button = new ToggleButton
      {
         Content = content, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
         MinWidth = 22, Height = 23, Padding = new Thickness(4, 2), CornerRadius = new CornerRadius(4)
      };
      AutomationProperties.SetName(button, name);
      ToolTip.SetShowOnDisabled(button, true);
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
         button.IsEnabled = kind == _all || !included.HasFlag(_all);
         button.Classes.Set("inherited", inherited);
         var origin = group is null ? "" : inherited ? " · inherited" : " · local override";
         var help = $"{AutomationProperties.GetName(button)} · {(button.IsChecked == true ? "included" : "off")}{origin}";
         if(kind == _all) help += button.IsChecked == true ? ". Turn off to restore your choices." : ". Temporarily include all.";
         else if(!button.IsEnabled) help += ". Turn off A to change this switch.";
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
