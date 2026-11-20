using System.Text.RegularExpressions;

namespace Cloyd.Links;

/// <summary>
/// Where in a line of a terminal's output something is: from the character at <see cref="Start"/> up to, and not
/// including, the one at <see cref="End"/>, counted from 0. What a link covers, as one underline.
/// </summary>
readonly record struct SpanOfALine(int Start, int End)
{
   /// <summary>The span of the line that <paramref name="match"/> matched.</summary>
   public static SpanOfALine Of(Match match) => new(match.Index, match.Index + match.Length);

   /// <summary>Whether this span and <paramref name="other"/> share a character.</summary>
   public bool Overlaps(SpanOfALine other) => Start < other.End && other.Start < End;
}
