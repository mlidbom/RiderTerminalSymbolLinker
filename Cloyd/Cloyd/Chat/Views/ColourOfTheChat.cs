using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Cloyd.Chat.Views;

/// <summary>
/// A colour the chat is drawn in, such as that of Claude's text: one in Windows' light theme and one in its dark, as the
/// Claude desktop app has them. What it paints follows the theme as Windows switches it.
/// </summary>
/// <remarks>
/// It is a resource of the chat, under itself as the key, in the dictionary of each theme
/// (<see cref="ColoursOfTheChat.AsResources"/>): what it paints takes it from there, as a <c>DynamicResource</c> does in
/// XAML.
/// </remarks>
sealed class ColourOfTheChat(string inTheLightTheme, string inTheDarkTheme)
{
   /// <summary>The colour in Windows' light theme.</summary>
   public Color InTheLightTheme { get; } = Color.Parse(inTheLightTheme);

   /// <summary>The colour in Windows' dark theme.</summary>
   public Color InTheDarkTheme { get; } = Color.Parse(inTheDarkTheme);

   /// <summary>Has the text of <paramref name="element"/>, such as a text block or a run of text in one, drawn in this colour.</summary>
   public TElement AsTheForegroundOf<TElement>(TElement element) where TElement : StyledElement => Painting(element, TextElement.ForegroundProperty);

   /// <summary>Has <paramref name="border"/>'s ground drawn in this colour.</summary>
   public Border AsTheBackgroundOf(Border border) => Painting(border, Border.BackgroundProperty);

   /// <summary>Has <paramref name="border"/>'s border line drawn in this colour.</summary>
   public Border AsTheBorderOf(Border border) => Painting(border, Border.BorderBrushProperty);

   TElement Painting<TElement>(TElement element, StyledProperty<IBrush?> property) where TElement : StyledElement
   {
      element.Bind(property, element.GetResourceObservable(this));
      return element;
   }
}
