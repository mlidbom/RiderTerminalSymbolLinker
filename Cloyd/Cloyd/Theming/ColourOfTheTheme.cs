using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Cloyd.Theming;

/// <summary>
/// A colour Cloyd draws in, such as that of Claude's text in the chat: one in Windows' light theme and one in its dark.
/// What it paints follows the theme as Windows switches it.
/// </summary>
/// <remarks>
/// It is a resource of what it paints, or of a control around that, under itself as the key, in the dictionary of each
/// theme (<see cref="AsResources"/>): what it paints takes it from there, as a <c>DynamicResource</c> does in XAML.
/// </remarks>
sealed class ColourOfTheTheme(string inTheLightTheme, string inTheDarkTheme)
{
   /// <summary>The colour in Windows' light theme.</summary>
   public Color InTheLightTheme { get; } = Color.Parse(inTheLightTheme);

   /// <summary>The colour in Windows' dark theme.</summary>
   public Color InTheDarkTheme { get; } = Color.Parse(inTheDarkTheme);

   /// <summary>The colour in <paramref name="theme"/>, light or dark.</summary>
   public Color In(ThemeVariant theme) => theme == ThemeVariant.Dark ? InTheDarkTheme : InTheLightTheme;

   /// <summary>A style setter that paints <paramref name="property"/> with this colour while the style applies.</summary>
   public Setter AsASetterFor(StyledProperty<IBrush?> property) => new(property, new DynamicResourceExtension(this));

   /// <summary>Has the text of <paramref name="element"/>, such as a text block or a run of text in one, drawn in this colour.</summary>
   public TElement AsTheForegroundOf<TElement>(TElement element) where TElement : StyledElement => Painting(element, TextElement.ForegroundProperty);

   /// <summary>Has <paramref name="border"/>'s ground drawn in this colour.</summary>
   public Border AsTheBackgroundOf(Border border) => Painting(border, Border.BackgroundProperty);

   /// <summary>Has <paramref name="control"/>'s ground drawn in this colour, such as a window's or a tab control's.</summary>
   public TControl AsTheBackgroundOf<TControl>(TControl control) where TControl : TemplatedControl => Painting(control, TemplatedControl.BackgroundProperty);

   /// <summary>Has <paramref name="border"/>'s border line drawn in this colour.</summary>
   public Border AsTheBorderOf(Border border) => Painting(border, Border.BorderBrushProperty);

   /// <summary>
   /// <paramref name="colours"/> as resources, for the resources of a control that what they paint is in: a dictionary for
   /// each of Windows' themes, in which each colour is a brush under itself as the key.
   /// </summary>
   public static ResourceDictionary AsResources(IEnumerable<ColourOfTheTheme> colours)
   {
      var all = colours.ToList();
      return new ResourceDictionary
      {
         ThemeDictionaries =
         {
            [ThemeVariant.Light] = InThe(ThemeVariant.Light),
            [ThemeVariant.Dark] = InThe(ThemeVariant.Dark)
         }
      };

      ResourceDictionary InThe(ThemeVariant theme)
      {
         var resources = new ResourceDictionary();
         foreach(var colour in all) resources[colour] = new ImmutableSolidColorBrush(colour.In(theme));
         return resources;
      }
   }

   TElement Painting<TElement>(TElement element, StyledProperty<IBrush?> property) where TElement : StyledElement
   {
      element.Bind(property, element.GetResourceObservable(this));
      return element;
   }
}
