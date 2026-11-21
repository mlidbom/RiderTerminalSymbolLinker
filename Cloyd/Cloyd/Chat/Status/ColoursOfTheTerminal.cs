using Avalonia.Controls;
using Cloyd.Theming;

namespace Cloyd.Chat.Status;

/// <summary>
/// The shades Cloyd shows the terminal's eight colours in, where it shows what Claude Code drew in them, such as the
/// status line's stretches (<see cref="Hub.StretchOfTheStatusLine"/>): one in Windows' light theme and one in its dark,
/// each readable on the chat's ground, as a terminal's theme picks its own. White and black are greys, so that neither
/// vanishes on the ground of its own colour.
/// </summary>
static class ColoursOfTheTerminal
{
   static readonly Dictionary<string, ColourOfTheTheme> ByName = new()
   {
      ["black"] = new("#1F1E1D", "#9C9A95"),
      ["red"] = new("#C42B1C", "#FF6B6B"),
      ["green"] = new("#1A7F37", "#6CB46F"),
      ["yellow"] = new("#9A6700", "#E5C07B"),
      ["blue"] = new("#1A5BAD", "#7FA8F5"),
      ["magenta"] = new("#8E3B9A", "#D38BE0"),
      ["cyan"] = new("#0E7C86", "#56B6C2"),
      ["white"] = new("#6B6966", "#E8E7E3")
   };

   /// <summary>The shade of the colour the terminal names <paramref name="name"/>, such as <c>red</c>: null for a name it doesn't have, which shows in the default colour.</summary>
   public static ColourOfTheTheme? Named(string name) => ByName.GetValueOrDefault(name);

   /// <summary>The shades as resources, for the resources of a control that what they paint is in.</summary>
   public static ResourceDictionary AsResources() => ColourOfTheTheme.AsResources(ByName.Values);
}
