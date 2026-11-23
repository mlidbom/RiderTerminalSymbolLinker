using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>One dashboard filter and its local override mark. A check or cross distinguishes explicit
/// choices from inherited ones even when both choices would include the same sessions.</summary>
sealed class SessionFilterSwitch : ToggleButton
{
   protected override Type StyleKeyOverride => typeof(ToggleButton);
   readonly IncludedSessions _kind;
   readonly string _name;
   readonly TextBlock _overrideMark = new() { Width = 11, TextAlignment = TextAlignment.Center, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
   bool HasOverride => _overrideMark.Text is not null;
   public event Action? UseParentSetting;

   public SessionFilterSwitch(IncludedSessions kind, string name, Control icon)
   {
      _kind = kind;
      _name = name;
      Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { icon, _overrideMark } };
      HorizontalContentAlignment = HorizontalAlignment.Center;
      VerticalContentAlignment = VerticalAlignment.Center;
      MinWidth = 22;
      Height = 23;
      Margin = new Thickness(0, 0, 2, 0);
      Padding = new Thickness(4, 2);
      CornerRadius = new CornerRadius(4);
      AutomationProperties.SetName(this, name);
      KeyDown += (_, key) =>
      {
         if(key.Key != Key.Delete || !HasOverride) return;
         UseParentSetting?.Invoke();
         key.Handled = true;
      };
   }

   public void ShowIfSelectedOrOverridden(bool expanded)
   {
      _overrideMark.IsVisible = HasOverride || expanded && DataContext is DashboardGroup;
      IsVisible = expanded || IsChecked == true || HasOverride;
   }

   public void Display(DashboardSessionFilters filters, DashboardGroup? group, IncludedSessions effective)
   {
      var local = group is null ? null : filters.LocalSetting(group, _kind);
      var selected = effective.HasFlag(_kind);
      IsChecked = selected;
      _overrideMark.Text = local switch { true => "✓", false => "×", null => null };
      Classes.Set("inherited", group is not null && local is null);
      Classes.Set("localOn", local == true);
      Classes.Set("localOff", local == false);
      Classes.Set("whileAll", _kind != IncludedSessions.All && effective.HasFlag(IncludedSessions.All));
      if(local.HasValue) ContextMenu ??= ParentSettingMenu();
      else ContextMenu = null;

      var help = _name;
      if(group is not null)
         help += local switch { true => " · Override On.", false => " · Override Off.", null => selected ? " · Inherited On." : " · Inherited Off." };
      if(_kind == IncludedSessions.All && filters.AutomaticallyIncludesAll(group)) help += " All is active because no individual switches are on.";
      if(group is not null) help += " Click to cycle On, Off, Unset.";
      else if(_kind == IncludedSessions.All) help += " Temporarily include all sessions. Click again to restore your choices.";
      if(local.HasValue) help += " Right-click or press Delete to use the parent setting.";
      ToolTip.SetTip(this, help);
      AutomationProperties.SetHelpText(this, help);
   }

   ContextMenu ParentSettingMenu()
   {
      var inherit = new MenuItem { Header = "Use parent setting" };
      inherit.Click += (_, _) => UseParentSetting?.Invoke();
      return new ContextMenu { Items = { inherit } };
   }
}
