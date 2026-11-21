using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Chat.Views;

namespace Cloyd.Chat.Searching;

/// <summary>
/// The bar a search of the chat is typed into, at the top right of the conversation, as a browser's: what is searched
/// for, which match is shown of how many, and buttons to the next match, the one before, and to close it. Ctrl+F opens
/// it (<see cref="ChatOfASession"/>); Enter goes to the next match, below, as F3 does; Shift+Enter to the one before, above;
/// Escape closes it.
/// </summary>
sealed class FindBarOfTheChat : UserControl
{
   /// <summary>The tip of the button to the next match.</summary>
   const string TipOfTheNextButton = "The next match, below: Enter or F3";

   /// <summary>The tip of the button to the match before.</summary>
   const string TipOfThePreviousButton = "The match before, above: Shift+Enter or Shift+F3";

   /// <summary>The tip of the button that closes the bar.</summary>
   const string TipOfTheCloseButton = "Close: Escape";

   readonly TextBox _text = new() { Width = 220, PlaceholderText = "Find in the conversation", VerticalAlignment = VerticalAlignment.Center };
   readonly TextBlock _counted = ColoursOfTheChat.Grey.AsTheForegroundOf(new TextBlock { MinWidth = 70, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0) });

   public FindBarOfTheChat()
   {
      IsVisible = false;
      HorizontalAlignment = HorizontalAlignment.Right;
      VerticalAlignment = VerticalAlignment.Top;
      Margin = new Thickness(0, 8, 24, 0);
      _text.TextChanged += (_, _) => TextChanged?.Invoke(Text);
      _text.AddHandler(KeyDownEvent, ActOnItsKeys, RoutingStrategies.Tunnel);
      Content = ColoursOfTheChat.Border.AsTheBorderOf(ColoursOfTheChat.BubbleOfAPrompt.AsTheBackgroundOf(new Border
      {
         BorderThickness = new Thickness(1),
         CornerRadius = new CornerRadius(8),
         Padding = new Thickness(6, 4),
         Child = new StackPanel
         {
            Orientation = Orientation.Horizontal,
            Children = { _text, _counted, Button("↑", TipOfThePreviousButton, () => PreviousAsked?.Invoke()), Button("↓", TipOfTheNextButton, () => NextAsked?.Invoke()), Button("×", TipOfTheCloseButton, Close) }
         }
      }));
   }

   protected override Type StyleKeyOverride => typeof(UserControl);

   /// <summary>What is searched for.</summary>
   string Text => _text.Text ?? "";

   /// <summary>Whether it is open: shown at the top right of the conversation.</summary>
   public bool IsOpen => IsVisible;

   /// <summary>Which match is shown of how many, such as <c>3 of 17</c>, as it says beside the box: null while nothing is searched for.</summary>
   public string? Counted => _counted.Text;

   /// <summary>Whether its box has the keyboard, for Magnus to type what to search for.</summary>
   public bool HasTheKeyboard => _text.IsFocused;

   /// <summary>Raised as what is searched for changes, with it: empty when nothing is.</summary>
   public event Action<string>? TextChanged;

   /// <summary>Raised when Magnus asks for the next match, below.</summary>
   public event Action? NextAsked;

   /// <summary>Raised when Magnus asks for the match before, above.</summary>
   public event Action? PreviousAsked;

   /// <summary>Raised when the bar closes.</summary>
   public event Action? Closed;

   /// <summary>Opens the bar, and gives its box the keyboard, what it holds selected, to be typed over, as a browser's does.</summary>
   public void Open()
   {
      IsVisible = true;
      Dispatcher.UIThread.Post(() =>
      {
         _text.Focus();
         _text.SelectAll();
      }, DispatcherPriority.Loaded);
   }

   /// <summary>Says which match is shown of how many, <paramref name="counted"/>: nothing when it is null.</summary>
   public void Show(string? counted) => _counted.Text = counted;

   void Close()
   {
      IsVisible = false;
      Closed?.Invoke();
   }

   void ActOnItsKeys(object? sender, KeyEventArgs key)
   {
      switch(key)
      {
         case { Key: Key.Enter, KeyModifiers: KeyModifiers.None }:
            NextAsked?.Invoke();
            break;
         case { Key: Key.Enter, KeyModifiers: KeyModifiers.Shift }:
            PreviousAsked?.Invoke();
            break;
         case { Key: Key.Escape, KeyModifiers: KeyModifiers.None }:
            Close();
            break;
         default:
            return;
      }

      key.Handled = true;
   }

   static Button Button(string glyph, string tip, Action clicked)
   {
      var button = new Button { Content = glyph, Padding = new Thickness(6, 0), Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
      ToolTip.SetTip(button, tip);
      button.Click += (_, _) => clicked();
      return button;
   }
}
