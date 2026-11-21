using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Cloyd.Chat.Views;
using LiveMarkdown.Avalonia;

namespace Cloyd.Chat.Searching;

/// <summary>
/// How the chat paints what a search found, as a browser does: each match in yellow, the one shown in orange. Painted in
/// the text blocks the chat is drawn with, LiveMarkdown's <see cref="MarkdownTextBlock"/>s, which paint the ranges given
/// their highlights in the styles they inherit: Claude's markdown through its <see cref="MarkdownRenderer"/>, which
/// paints them again as what Claude writes grows.
/// </summary>
static class MatchesPainted
{
   /// <summary>The name of the highlight of each match.</summary>
   public const string AMatch = "a-match";

   /// <summary>The name of the highlight of the match shown, painted over that of each match.</summary>
   public const string TheMatchShown = "the-match-shown";

   /// <summary>How each is painted in <paramref name="theme"/>: for the chat's <see cref="MarkdownTextBlock.HighlightStylesProperty"/>, which its blocks inherit.</summary>
   public static TextHighlightStyles StylesIn(ThemeVariant theme)
   {
      var styles = new TextHighlightStyles();
      styles.Set(AMatch, new TextHighlightStyle { Background = new SolidColorBrush(ColoursOfTheChat.AMatch.In(theme)), CornerRadius = new CornerRadius(2) });
      styles.Set(TheMatchShown, new TextHighlightStyle { Background = new SolidColorBrush(ColoursOfTheChat.TheMatchShown.In(theme)), CornerRadius = new CornerRadius(2) });
      return styles;
   }

   /// <summary>Paints each match of <paramref name="pattern"/> in <paramref name="view"/>, the view of something the chat shows; or none, when it is null.</summary>
   public static void In(Control view, TextSearchPattern? pattern)
   {
      foreach(var text in TextsIn(view))
         switch(text)
         {
            case MarkdownRenderer renderer when pattern is not null:
               renderer.ApplyTextSearch(pattern, AMatch);
               break;
            case MarkdownRenderer renderer:
               renderer.ClearTextSearch();
               break;
            case MarkdownTextBlock block when pattern is not null && RangesIn(block, pattern) is { Count: > 0 } ranges:
               block.Highlights.Set(AMatch, ranges, priority: 0);
               break;
            case MarkdownTextBlock block:
               block.Highlights.Remove(AMatch);
               break;
         }
   }

   /// <summary>
   /// Paints the match at <paramref name="occurrence"/> of those of <paramref name="pattern"/> in <paramref name="view"/> as
   /// the one shown, and returns it: the last when the view has fewer, as markdown rendered may; null when it has none
   /// yet, as Claude's markdown before it is rendered.
   /// </summary>
   public static TextHighlightMatch? AsTheOneShown(Control view, TextSearchPattern pattern, int occurrence)
   {
      var matches = MatchesIn(view, pattern);
      if(matches.Count == 0) return null;
      var match = matches[Math.Min(occurrence, matches.Count - 1)];
      match.Block.Highlights.Set(TheMatchShown, [match.Range], priority: 1);
      return match;
   }

   /// <summary>Paints <paramref name="match"/> as one match again, no longer the one shown.</summary>
   public static void NoLongerShown(TextHighlightMatch match) => match.Block.Highlights.Remove(TheMatchShown);

   /// <summary>Scrolls the chat to <paramref name="match"/>, with some of what is around it in view.</summary>
   public static void ScrollTo(TextHighlightMatch match)
   {
      var bounds = match.Block.GetTextRangeBoundsInControl(match.Range.Start, match.Range.Length).Aggregate((all, it) => all.Union(it));
      match.Block.BringIntoView(bounds.Inflate(new Thickness(0, 60)));
   }

   /// <summary>The matches of <paramref name="pattern"/> in <paramref name="view"/>, in order: of Claude's markdown as rendered.</summary>
   static List<TextHighlightMatch> MatchesIn(Control view, TextSearchPattern pattern) =>
      [.. TextsIn(view).SelectMany(text => text switch
      {
         MarkdownRenderer renderer => renderer.ApplyTextSearch(pattern, AMatch),
         MarkdownTextBlock block => RangesIn(block, pattern).Select(range => new TextHighlightMatch(block, range)),
         _ => []
      })];

   static List<TextHighlightRange> RangesIn(MarkdownTextBlock block, TextSearchPattern pattern) => [.. pattern.FindRanges(block.LayoutText)];

   /// <summary>The text in <paramref name="view"/>: its markdown renderers, whose blocks they search themselves, and its other text blocks, in order.</summary>
   static IEnumerable<StyledElement> TextsIn(ILogical view) => view switch
   {
      MarkdownRenderer renderer => [renderer],
      MarkdownTextBlock block => [block],
      _ => view.LogicalChildren.SelectMany(TextsIn)
   };
}
