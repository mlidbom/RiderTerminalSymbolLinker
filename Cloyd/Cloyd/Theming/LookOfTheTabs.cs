using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Cloyd.Theming;

/// <summary>
/// How a project window's tabs look, as tabs of what they show: on a strip a shade darker than the window's ground, the
/// selected tab in the window's ground, joined to what it shows below it, as a browser's. Magnus, 2026-11-21: "it sort of
/// feels like the tabs are just hovering in space rather than being tabs for something".
/// </summary>
/// <remarks>
/// Through the resources Avalonia's Fluent theme draws its tabs with, such as <c>TabItemHeaderBackgroundSelected</c> and
/// <c>TabItemHeaderFontSize</c>, in the window's resources: the theme's own templates are kept.
/// </remarks>
static class LookOfTheTabs
{
   /// <summary>The window's ground: the selected tab's, and what it shows.</summary>
   public static readonly ColourOfTheTheme Ground = new("#FFFFFF", "#1F1F1F");

   /// <summary>The strip the tabs are on, behind the tabs not selected.</summary>
   public static readonly ColourOfTheTheme Strip = new("#E9E9E7", "#141414");

   /// <summary>A tab not selected, under the mouse.</summary>
   static readonly ColourOfTheTheme UnderTheMouse = new("#DCDCDA", "#2A2A2A");

   /// <summary>The size of a tab's text, in logical pixels: the theme's is 24, which Magnus found very large; 14 a little small.</summary>
   public const double SizeOfTheText = 15;

   /// <summary>How high a tab is, in logical pixels: what the buttons at the strip's start are as high as.</summary>
   public const double HowHighATabIs = 34;

   /// <summary>The room above the tabs on the strip, in logical pixels.</summary>
   public const double RoomAboveTheTabs = 4;

   /// <summary>
   /// The resources the theme draws the tabs with, the tabs starting <paramref name="roomBeforeTheTabs"/> logical pixels
   /// into the strip, after what is before them, such as the buttons that start a session and open the history.
   /// </summary>
   public static ResourceDictionary AsResources(double roomBeforeTheTabs)
   {
      var resources = new ResourceDictionary
      {
         ["TabItemHeaderFontSize"] = SizeOfTheText,
         ["TabItemMinHeight"] = HowHighATabIs,
         ["TabItemHeaderMinHeight"] = HowHighATabIs,
         ["TabItemHeaderCornerRadius"] = new CornerRadius(8, 8, 0, 0),
         ["TabControlTopPlacementItemMargin"] = new Thickness(roomBeforeTheTabs, RoomAboveTheTabs, 0, 0),
         ThemeDictionaries =
         {
            [ThemeVariant.Light] = InThe(ThemeVariant.Light),
            [ThemeVariant.Dark] = InThe(ThemeVariant.Dark)
         }
      };
      resources.MergedDictionaries.Add(ColourOfTheTheme.AsResources([Ground, Strip]));
      return resources;

      static ResourceDictionary InThe(ThemeVariant theme) => new()
      {
         ["TabItemHeaderBackgroundSelected"] = BrushOf(Ground, theme),
         ["TabItemHeaderBackgroundSelectedPointerOver"] = BrushOf(Ground, theme),
         ["TabItemHeaderBackgroundSelectedPressed"] = BrushOf(Ground, theme),
         ["TabItemHeaderBackgroundUnselected"] = Brushes.Transparent,
         ["TabItemHeaderBackgroundUnselectedPointerOver"] = BrushOf(UnderTheMouse, theme),
         ["TabItemHeaderBackgroundUnselectedPressed"] = BrushOf(UnderTheMouse, theme)
      };

      static IBrush BrushOf(ColourOfTheTheme colour, ThemeVariant theme) => new ImmutableSolidColorBrush(colour.In(theme));
   }
}
