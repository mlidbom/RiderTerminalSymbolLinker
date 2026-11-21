using LiveMarkdown.Avalonia;

namespace Cloyd.Chat.Searching;

/// <summary>
/// What a search of the chat found, as Ctrl+F searches it: each match of <see cref="Pattern"/> in the text the chat shows
/// of the conversation (<see cref="ShownInTheChat.TextShown"/>), drawn or not, and the one the chat shows, which Magnus
/// moves from match to match. Numbered down the conversation, as a browser numbers the matches down a page: the next
/// match is the one below (Magnus, 2026-11-21: "I want it to go down please").
/// </summary>
/// <remarks>Case doesn't matter, as in a browser's search.</remarks>
sealed class FoundInTheChat
{
   readonly int _shown;

   FoundInTheChat(TextSearchPattern pattern, IReadOnlyList<MatchInTheChat> matches, int shown)
   {
      Pattern = pattern;
      Matches = matches;
      _shown = shown;
   }

   /// <summary>
   /// What <paramref name="text"/> finds in <paramref name="conversation"/>, what the chat shows: the match shown the first
   /// from <paramref name="firstInView"/> on, the place of the first thing in view, as a browser shows the first match from
   /// the top of the page in view; else the last above it, the nearest, rather than one back at the conversation's start.
   /// </summary>
   public static FoundInTheChat Of(string text, IReadOnlyList<ShownInTheChat> conversation, int firstInView)
   {
      var pattern = new TextSearchPattern(text);
      var matches = MatchesIn(conversation, pattern);
      var firstFromTheView = matches.TakeWhile(it => it.Shown < firstInView).Count();
      return new(pattern, matches, Math.Max(0, Math.Min(firstFromTheView, matches.Count - 1)));
   }

   /// <summary>What is searched for.</summary>
   public TextSearchPattern Pattern { get; }

   /// <summary>The matches, in the conversation's order.</summary>
   IReadOnlyList<MatchInTheChat> Matches { get; }

   /// <summary>The match the chat shows: null when there is none.</summary>
   public MatchInTheChat? Shown => Matches.Count > 0 ? Matches[_shown] : null;

   /// <summary>Which match the chat shows, of how many, such as <c>3 of 17</c>; or that there are none.</summary>
   public string Counted => Matches.Count > 0 ? $"{_shown + 1} of {Matches.Count}" : "No matches";

   /// <summary>The search, showing the next match, below the one shown: the first again after the last, as F3 goes.</summary>
   public FoundInTheChat Next() => Showing(_shown + 1);

   /// <summary>The search, showing the match before, above the one shown: the last after the first, as Shift+F3 goes.</summary>
   public FoundInTheChat Previous() => Showing(_shown - 1);

   /// <summary>
   /// The same search in <paramref name="conversation"/>, what the chat shows now, as the conversation goes on: showing the
   /// same match while it is still found, else the one nearest the end.
   /// </summary>
   public FoundInTheChat In(IReadOnlyList<ShownInTheChat> conversation)
   {
      var matches = MatchesIn(conversation, Pattern);
      var shown = Shown is {} match ? IndexOf(matches, match) : -1;
      return new(Pattern, matches, shown >= 0 ? shown : Math.Max(0, matches.Count - 1));
   }

   FoundInTheChat Showing(int shown) => Matches.Count == 0 ? this : new(Pattern, Matches, (shown + Matches.Count) % Matches.Count);

   static IReadOnlyList<MatchInTheChat> MatchesIn(IReadOnlyList<ShownInTheChat> conversation, TextSearchPattern pattern) =>
      [.. Enumerable.Range(0, conversation.Count).SelectMany(at => Enumerable.Range(0, pattern.FindRanges(conversation[at].TextShown).Count()).Select(occurrence => new MatchInTheChat(at, occurrence)))];

   static int IndexOf(IReadOnlyList<MatchInTheChat> matches, MatchInTheChat match)
   {
      for(var at = 0; at < matches.Count; at++)
         if(matches[at] == match) return at;
      return -1;
   }
}
