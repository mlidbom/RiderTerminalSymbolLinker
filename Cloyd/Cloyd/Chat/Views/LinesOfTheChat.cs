using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Cloyd.Theming;
using LiveMarkdown.Avalonia;

namespace Cloyd.Chat.Views;

/// <summary>The plain parts the chat is drawn with: bubbles, grey lines, monospaced text, and lines that open and close what is under them.</summary>
/// <remarks>
/// The text of a bubble, a grey line and a line that opens is in LiveMarkdown's <see cref="MarkdownTextBlock"/>, as Claude's
/// markdown is, so that a search of the chat paints its matches in them all alike (<see cref="Searching.MatchesPainted"/>).
/// </remarks>
static class LinesOfTheChat
{
   /// <summary>The widest a bubble grows, as its text grows: about three quarters of the column.</summary>
   const double WidestBubble = 580;

   public static readonly FontFamily Monospaced = new("Cascadia Mono, Consolas, monospace");

   /// <summary>A prompt's bubble, at the right of the column, its text in <paramref name="inlines"/>.</summary>
   public static Control Bubble(params Inline[] inlines) => Bubble(int.MaxValue, inlines);

   /// <summary>A prompt's bubble, at the right of the column, its text in <paramref name="inlines"/>: at most <paramref name="mostLines"/> lines of it, cut with an ellipsis.</summary>
   public static Control Bubble(int mostLines, params Inline[] inlines)
   {
      var text = ColoursOfTheChat.Text.AsTheForegroundOf(new MarkdownTextBlock { TextWrapping = TextWrapping.Wrap, MaxLines = mostLines, TextTrimming = TextTrimming.CharacterEllipsis });
      text.Inlines!.AddRange(inlines);
      return PromptBubble(text);
   }

   public static Border PromptBubble(Control message) => ColoursOfTheChat.BubbleOfAPrompt.AsTheBackgroundOf(new Border
   {
      CornerRadius = new CornerRadius(12),
      Padding = new Thickness(14, 10),
      HorizontalAlignment = HorizontalAlignment.Right,
      MaxWidth = WidestBubble,
      Child = message
   });

   /// <summary>A grey line, such as <c>Compacted session</c>.</summary>
   public static Control Grey(string line) => ColoursOfTheChat.Grey.AsTheForegroundOf(new MarkdownTextBlock { Text = line, TextWrapping = TextWrapping.Wrap });

   /// <summary>A left-aligned bubble for an agent's message. Long replies can use the full conversation column.</summary>
   public static Border AgentBubble(Control message, ColourOfTheTheme ground) => ground.AsTheBackgroundOf(new Border
   {
      HorizontalAlignment = HorizontalAlignment.Left,
      CornerRadius = new CornerRadius(4, 12, 12, 12),
      Padding = new Thickness(14, 9),
      Child = message
   });

   /// <summary><paramref name="text"/> in the monospaced font, selectable, as a tool's output is shown: in <paramref name="colour"/>, or the chat's text colour.</summary>
   public static SelectableTextBlock InMonospace(string text, ColourOfTheTheme? colour = null) =>
      (colour ?? ColoursOfTheChat.Text).AsTheForegroundOf(new SelectableTextBlock { Text = text, FontFamily = Monospaced, FontSize = 12, TextWrapping = TextWrapping.Wrap });

   /// <summary>The text <paramref name="block"/> shows, its runs one after another, such as a line made of a bold part and a grey part.</summary>
   public static string TextIn(TextBlock block) => block.Inlines!.Aggregate("", (shown, it) => shown + ((Run)it).Text);

   /// <summary>A run of text in <paramref name="colour"/>.</summary>
   public static Run Coloured(string text, ColourOfTheTheme colour) => colour.AsTheForegroundOf(new Run(text));

   /// <summary>
   /// A line made of <paramref name="inlines"/>, followed by a chevron, that opens what <paramref name="opened"/> makes
   /// under it when clicked, and closes it when clicked again: <paramref name="isOpen"/> at first, and
   /// <paramref name="toggled"/> told each time. With <paramref name="singleLineWhenFolded"/>, only the first
   /// line of the header takes space until the row opens.
   /// </summary>
   public static Control Opening(Inline[] inlines, Func<Control> opened, bool isOpen, Action<bool> toggled, string? accessibleName = null, bool singleLineWhenFolded = false)
   {
      var text = ColoursOfTheChat.Grey.AsTheForegroundOf(new MarkdownTextBlock { TextWrapping = TextWrapping.Wrap, IsHitTestVisible = false });
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
      if(accessibleName is not null) AutomationProperties.SetName(line, accessibleName);
      if(singleLineWhenFolded)
      {
         line.MinHeight = 0;
         ToolTip.SetTip(line, accessibleName);
         text.TextTrimming = TextTrimming.CharacterEllipsis;
      }
      foreach(var key in new[] { "ButtonBackgroundPointerOver", "ButtonBackgroundPressed" }) line.Resources[key] = Brushes.Transparent;
      var under = new ContentControl();
      Show(isOpen);
      line.Click += (_, _) =>
      {
         if(line.FindAncestorOfType<SessionChat>() is {} chat) chat.KeepHeaderInPlace(line, Toggle);
         else Toggle();
      };
      return new StackPanel { Spacing = 6, Children = { line, under } };

      void Toggle()
      {
         Show(under.Content is null);
         toggled(under.Content is not null);
      }

      void Show(bool open)
      {
         under.Content = open ? opened() : null;
         if(singleLineWhenFolded)
         {
            under.IsVisible = open;
            text.MaxLines = open ? int.MaxValue : 1;
         }
         chevron.Text = open ? "  ⌄" : "  ›";
      }
   }
}
