using LiveMarkdown.Avalonia;

namespace Cloyd.Chat.Searching;

/// <summary>
/// What a search of the chat found, as Ctrl+F searches it: each match of <see cref="Pattern"/> in the text the chat shows
/// of the conversation (<see cref="ShownInTheChat.TextShown"/>), drawn or not, and the one the chat shows, which Magnus
/// moves from match to match. Numbered from the conversation's end, the nearest it first, as a search of it goes back in
/// time: the next match is the one above.
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

   /// <summary>What <paramref name="text"/> finds in <paramref name="conversation"/>, what the chat shows: the match nearest its end shown.</summary>
   public static FoundInTheChat Of(string text, IReadOnlyList<ShownInTheChat> conversation)
   {
      var pattern = new TextSearchPattern(text);
      return new(pattern, MatchesIn(conversation, pattern), shown: 0);
   }

   /// <summary>What is searched for.</summary>
   public TextSearchPattern Pattern { get; }

   /// <summary>The matches, the nearest the conversation's end first.</summary>
   IReadOnlyList<MatchInTheChat> Matches { get; }

   /// <summary>The match the chat shows: null when there is none.</summary>
   public MatchInTheChat? Shown => Matches.Count > 0 ? Matches[_shown] : null;

   /// <summary>Which match the chat shows, of how many, such as <c>3 of 17</c>; or that there are none.</summary>
   public string Counted => Matches.Count > 0 ? $"{_shown + 1} of {Matches.Count}" : "No matches";

   /// <summary>The search, showing the next match, above the one shown: the first again after the last, as F3 goes.</summary>
   public FoundInTheChat Next() => Showing(_shown + 1);

   /// <summary>The search, showing the match before, below the one shown: the last after the first, as Shift+F3 goes.</summary>
   public FoundInTheChat Previous() => Showing(_shown - 1);

   /// <summary>
   /// The same search in <paramref name="conversation"/>, what the chat shows now, as the conversation goes on: showing the
   /// same match while it is still found, else the one nearest the end.
   /// </summary>
   public FoundInTheChat In(IReadOnlyList<ShownInTheChat> conversation)
   {
      var matches = MatchesIn(conversation, Pattern);
      var shown = Shown is {} match ? IndexOf(matches, match) : -1;
      return new(Pattern, matches, Math.Max(shown, 0));
   }

   FoundInTheChat Showing(int shown) => Matches.Count == 0 ? this : new(Pattern, Matches, (shown + Matches.Count) % Matches.Count);

   static IReadOnlyList<MatchInTheChat> MatchesIn(IReadOnlyList<ShownInTheChat> conversation, TextSearchPattern pattern) =>
      [.. Enumerable.Range(0, conversation.Count).Reverse().SelectMany(at => Enumerable.Range(0, pattern.FindRanges(conversation[at].TextShown).Count()).Reverse().Select(occurrence => new MatchInTheChat(at, occurrence)))];

   static int IndexOf(IReadOnlyList<MatchInTheChat> matches, MatchInTheChat match)
   {
      for(var at = 0; at < matches.Count; at++)
         if(matches[at] == match) return at;
      return -1;
   }
}
