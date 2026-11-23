using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Cloyd.ProjectsDashboard;

sealed partial class WorktreeRemovalDialog
{
   bool Dark => ActualThemeVariant == ThemeVariant.Dark;
   IBrush Ink => Dark ? Brushes.WhiteSmoke : Brush.Parse("#20242B");
   IBrush Muted => Dark ? Brushes.LightGray : Brush.Parse("#586371");
   IBrush Line => Brush.Parse(Dark ? "#46494D" : "#D6DADE");
   IBrush Surface => Brush.Parse(Dark ? "#26282B" : "#F6F7F8");
   IBrush White => Brush.Parse(Dark ? "#202124" : "#FFFFFF");
   IBrush Green => Brush.Parse(Dark ? "#82D4A2" : "#206541");
   IBrush Amber => Brush.Parse(Dark ? "#FFDC82" : "#805000");
   static readonly IBrush Red = Brush.Parse("#A51B26"), Blue = Brush.Parse("#0067AB");
   static readonly Geometry WarningIcon = Geometry.Parse("M10 1 L20 19 H0 Z M9 7 V12 H11 V7 Z M9 14 V16 H11 V14 Z");

   Border Notice(string heading, string detail, bool danger)
   {
      var text = Stack(4);
      var ink = danger ? Dark ? Brushes.LightPink : Red : Amber;
      text.Children.Add(Label(heading, 15, FontWeight.Bold, ink));
      text.Children.Add(Label(detail, 13));
      var grid = Grid("19,*", 10);
      Put(grid, new PathIcon { Data = WarningIcon, Foreground = ink, Width = 17, Height = 17, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0,2,0,0) }, 0);
      Put(grid, text, 1);
      return new Border { Padding = new Thickness(12,10), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(3,0,0,0), BorderBrush = ink,
         Background = Brush.Parse(Dark ? danger ? "#49242C" : "#413722" : danger ? "#FDECEE" : "#FFF4DC"), Child = grid };
   }

   static Button ActionButton(string title, Action action, bool enabled)
   {
      var button = new Button { Content = title, IsEnabled = enabled, Padding = new Thickness(12,5), MinHeight = 30, FontSize = 13,
         HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Center };
      button.Click += (_, _) => action();
      return button;
   }
   static StackPanel Stack(double spacing) => new() { Spacing = spacing };
   StackPanel Field(string title, string value, IBrush color)
   {
      var field = Stack(1);
      field.Children.Add(Label(title, 12, color: Muted));
      field.Children.Add(Label(value, 13, FontWeight.SemiBold, color));
      return field;
   }
   Border Card(Control child) => new() { BorderBrush = Line, BorderThickness = new Thickness(1), Background = White, CornerRadius = new CornerRadius(6), Child = child };
   Border Divider() => new() { Background = Line, Height = 1 };
   StackPanel Section(string heading, Control control)
   {
      var section = Stack(6);
      section.Children.Add(Label(heading, 17, FontWeight.SemiBold));
      section.Children.Add(control);
      return section;
   }
   static Grid Grid(string columns, double spacing) => new() { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = spacing };
   static void Put(Grid grid, Control control, int column) { Avalonia.Controls.Grid.SetColumn(control,column); grid.Children.Add(control); }
   TextBlock Label(string text, double size, FontWeight? weight = null, IBrush? color = null) => new()
   { Text = text, FontSize = size, FontWeight = weight ?? FontWeight.Normal, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap };
}
