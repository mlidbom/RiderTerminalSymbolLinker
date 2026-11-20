using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cloyd.Chat.Views;

/// <summary>The plain parts the chat is drawn with: bubbles, grey lines, monospaced text, and lines that open and close what is under them.</summary>
static class LinesOfTheChat
{
   /// <summary>The widest a bubble grows, as its text grows: about three quarters of the column.</summary>
   const double WidestBubble = 580;

   public static readonly FontFamily Monospaced = new("Cascadia Mono, Consolas, monospace");

   /// <summary>A prompt's bubble, at the right of the column, its text in <paramref name="inlines"/>.</summary>
   public static Control Bubble(params Inline[] inlines)
   {
      var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, Foreground = ColoursOfTheChat.Text };
      text.Inlines!.AddRange(inlines);
      return new Border
      {
         Background = ColoursOfTheChat.BubbleOfAPrompt,
         CornerRadius = new CornerRadius(12),
         Padding = new Thickness(14, 10),
         HorizontalAlignment = HorizontalAlignment.Right,
         MaxWidth = WidestBubble,
         Child = text
      };
   }

   /// <summary>A grey line, such as <c>Compacted session</c>.</summary>
   public static Control Grey(string line) => new TextBlock { Text = line, Foreground = ColoursOfTheChat.Grey, TextWrapping = TextWrapping.Wrap };

   /// <summary><paramref name="text"/> in the monospaced font, selectable, as a tool's output is shown.</summary>
   public static SelectableTextBlock InMonospace(string text, IBrush? foreground = null) =>
      new() { Text = text, FontFamily = Monospaced, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = foreground ?? ColoursOfTheChat.Text };

   /// <summary>A run of text in <paramref name="brush"/>.</summary>
   public static Run Coloured(string text, IBrush brush) => new(text) { Foreground = brush };

   /// <summary>
   /// A line made of <paramref name="inlines"/>, followed by a chevron, that opens what <paramref name="opened"/> makes
   /// under it when clicked, and closes it when clicked again: <paramref name="isOpen"/> at first, and
   /// <paramref name="toggled"/> told each time.
   /// </summary>
   public static Control Opening(Inline[] inlines, Func<Control> opened, bool isOpen, Action<bool>? toggled = null)
   {
      var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = ColoursOfTheChat.Grey };
      var chevron = Coloured("", ColoursOfTheChat.Grey);
      text.Inlines!.AddRange(inlines);
      text.Inlines.Add(chevron);
      var line = new Button
      {
         Content = text,
         Background = Brushes.Transparent,
         BorderThickness = new Thickness(0),
         Padding = new Thickness(0),
         HorizontalAlignment = HorizontalAlignment.Left,
         Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
      };
      foreach(var key in new[] { "ButtonBackgroundPointerOver", "ButtonBackgroundPressed" }) line.Resources[key] = Brushes.Transparent;
      var under = new ContentControl();
      Show(isOpen);
      line.Click += (_, _) =>
      {
         Show(under.Content is null);
         toggled?.Invoke(under.Content is not null);
      };
      return new StackPanel { Spacing = 6, Children = { line, under } };

      void Show(bool open)
      {
         under.Content = open ? opened() : null;
         chevron.Text = open ? "  ⌄" : "  ›";
      }
   }
}
