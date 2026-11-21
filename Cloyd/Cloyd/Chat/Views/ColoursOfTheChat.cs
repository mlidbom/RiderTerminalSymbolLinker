using Avalonia.Controls;
using Avalonia.Styling;
using Cloyd.Theming;
using TextMateSharp.Grammars;

namespace Cloyd.Chat.Views;

/// <summary>
/// The chat's colours, near those of the Claude desktop app (<c>dev_docs/cloyd-chat-specification.md</c>): in Windows'
/// light theme as its pictures show it, such as <c>05-group-opened.png</c>, and in its dark theme as
/// <c>19-dark-theme.png</c> does. The chat follows Windows' theme, as the app does.
/// </summary>
static class ColoursOfTheChat
{
   /// <summary>Claude's text, a prompt's, and what a tool call acted on.</summary>
   public static readonly ColourOfTheTheme Text = new("#1F1E1D", "#E8E7E3");

   /// <summary>The grey lines: a run of tool calls counted, a call's verb, a compaction.</summary>
   public static readonly ColourOfTheTheme Grey = new("#6B6966", "#9C9A95");

   /// <summary>The bubble of a prompt of Magnus's.</summary>
   public static readonly ColourOfTheTheme BubbleOfAPrompt = new("#F3F3F2", "#30302E");

   /// <summary>A slash command in its bubble.</summary>
   public static readonly ColourOfTheTheme SlashCommand = new("#1A5BAD", "#7FA8F5");

   /// <summary>A tool call that failed: its <c>Failed</c>.</summary>
   public static readonly ColourOfTheTheme Failed = new("#AE2539", "#E5605A");

   /// <summary>The lines an edit put in: <c>+9</c>, and each such line of a change.</summary>
   public static readonly ColourOfTheTheme Added = new("#3A9E4F", "#6CB46F");

   /// <summary>The lines an edit took out.</summary>
   public static readonly ColourOfTheTheme Removed = new("#AE2539", "#E5605A");

   /// <summary>The ground of a line an edit put in.</summary>
   public static readonly ColourOfTheTheme GroundOfAnAddedLine = new("#E3F4E6", "#1E3A22");

   /// <summary>The ground of a line an edit took out.</summary>
   public static readonly ColourOfTheTheme GroundOfARemovedLine = new("#FBE6E4", "#3F1F1E");

   /// <summary>The thin border of an opened run of tool calls, a command, a code block, and the lines between the rows.</summary>
   public static readonly ColourOfTheTheme Border = new("#E5E4E1", "#3A3936");

   /// <summary>The ground of a code block in Claude's markdown.</summary>
   public static readonly ColourOfTheTheme GroundOfACodeBlock = new("#FBFBFA", "#1F1F1E");

   /// <summary>The ground of a match a search of the chat found, as a browser paints it.</summary>
   public static readonly ColourOfTheTheme AMatch = new("#FBE38E", "#5E5020");

   /// <summary>The ground of the match a search shows, over that of the other matches.</summary>
   public static readonly ColourOfTheTheme TheMatchShown = new("#F7A35C", "#9C5A1C");

   static readonly ColourOfTheTheme[] All = [Text, Grey, BubbleOfAPrompt, SlashCommand, Failed, Added, Removed, GroundOfAnAddedLine, GroundOfARemovedLine, Border, GroundOfACodeBlock, AMatch, TheMatchShown];

   /// <summary>
   /// The colours LiveMarkdown.Avalonia draws Claude's markdown in, by the keys of its <c>Defaults.axaml</c>, which are
   /// those of a dark theme: its text, inline code and quotes in the chat's text colour, its borders in the chat's, and
   /// a code block's ground.
   /// </summary>
   static readonly (string Key, ColourOfTheTheme Colour)[] ColoursOfClaudesMarkdown =
   [
      ("ForegroundColor", Text),
      ("CodeInlineColor", Text),
      ("QuoteBorderColor", Border),
      ("BorderColor", Border),
      ("SecondaryCardBackgroundColor", GroundOfACodeBlock)
   ];

   /// <summary>The colours of the code in Claude's markdown, as Visual Studio Code's default theme has them in <paramref name="theme"/>.</summary>
   public static ThemeName OfCodeIn(ThemeVariant theme) => theme == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;

   /// <summary>The chat's colours as resources: a dictionary for each of Windows' themes, from which what the chat draws takes its colours.</summary>
   public static ResourceDictionary AsResources()
   {
      var resources = ColourOfTheTheme.AsResources(All);
      foreach(var (theme, inTheTheme) in resources.ThemeDictionaries)
         foreach(var (key, colour) in ColoursOfClaudesMarkdown)
            ((ResourceDictionary)inTheTheme)[key] = colour.In(theme);
      return resources;
   }
}
