using LiveMarkdown.Avalonia;

namespace Cloyd.Chat.Views;

/// <summary>Renders a draft, a sent prompt or a reply with the chat's Markdown and code colours.</summary>
static class ChatMarkdown
{
   public static MarkdownRenderer Render(string markdown)
   {
      var text = new ObservableStringBuilder();
      text.Append(markdown);
      var renderer = ColoursOfTheChat.Text.AsTheForegroundOf(new MarkdownRenderer { MarkdownBuilder = text });
      renderer.AttachedToVisualTree += (_, _) => ColourTheCode();
      renderer.ActualThemeVariantChanged += (_, _) => ColourTheCode();
      return renderer;

      void ColourTheCode() => renderer.CodeBlockColorTheme = ColoursOfTheChat.OfCodeIn(renderer.ActualThemeVariant);
   }
}
