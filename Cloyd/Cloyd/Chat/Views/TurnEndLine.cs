using Avalonia.Controls;
using Avalonia.Media;
using LiveMarkdown.Avalonia;

namespace Cloyd.Chat.Views;

/// <summary>The timing or outcome of a finished turn with a rule separating it from the next turn.</summary>
sealed class TurnEndLine : StackPanel
{
   public TurnEndLine(string text)
   {
      Spacing = 1;
      Children.Add(ColoursOfTheChat.Text.AsTheForegroundOf(new MarkdownTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold }));
      Children.Add(ColoursOfTheChat.Text.AsTheBackgroundOf(new Border { Height = 1.5, UseLayoutRounding = false }));
   }
}
