using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cloyd.Sessions;

/// <summary>The header shared by the project window's horizontal tabs and Projects Dashboard's vertical tabs.</summary>
sealed class SessionTabHeader : Grid
{
   public static readonly StyledProperty<SessionTabHeading?> HeadingProperty = AvaloniaProperty.Register<SessionTabHeader, SessionTabHeading?>(nameof(Heading));
   public static readonly StyledProperty<bool> CanEndProperty = AvaloniaProperty.Register<SessionTabHeader, bool>(nameof(CanEnd), true);
   readonly TextBlock _title = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
   readonly Button _end = new() { Content = "×", Background = Brushes.Transparent, Padding = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center };
   readonly Border _beforeClose = new();
   /// <summary>An optional session action beside the close button, such as pinning a dashboard tab.</summary>
   public Control? BeforeClose { get => _beforeClose.Child; set => _beforeClose.Child = value; }
   public IndicatorOfASessionTab Indicator { get; } = new();
   public SessionTabHeading? Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
   public bool CanEnd { get => GetValue(CanEndProperty); set => SetValue(CanEndProperty, value); }
   public event EventHandler<Avalonia.Interactivity.RoutedEventArgs>? EndRequested;

   public SessionTabHeader()
   {
      ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
      ColumnSpacing = 3;
      SetColumn(_title, 1);
      var actions = new StackPanel { Orientation = Orientation.Horizontal, Children = { _beforeClose, _end } };
      SetColumn(actions, 2);
      Children.Add(Indicator);
      Children.Add(_title);
      Children.Add(actions);
      ToolTip.SetTip(_end, "End the session");
      _end.Click += (_, e) => EndRequested?.Invoke(this, e);
   }

   protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
   {
      base.OnPropertyChanged(change);
      if(change.Property == CanEndProperty) _end.IsVisible = CanEnd;
      if(change.Property != HeadingProperty) return;
      _title.Text = Heading?.Title;
      Indicator.Show(Heading?.Agent, Heading?.Running);
   }
}
