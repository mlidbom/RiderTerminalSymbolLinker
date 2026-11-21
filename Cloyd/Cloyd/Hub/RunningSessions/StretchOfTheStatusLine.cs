using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;

namespace Cloyd.Hub.RunningSessions;

/// <summary>
/// A stretch of a Claude Code's status line all in one style, as the terminal shows it: such as a weekly limit nearly used
/// up, "week 100%", in bold red.
/// </summary>
/// <param name="Text">Its text.</param>
/// <param name="Colour">
/// The colour of its text, as one of the terminal's eight colours names it: <c>black</c>, <c>red</c>, <c>green</c>,
/// <c>yellow</c>, <c>blue</c>, <c>magenta</c>, <c>cyan</c> or <c>white</c>. Null for the default. The shade is Cloyd's
/// to choose, as a terminal's theme chooses it.
/// </param>
/// <param name="IsBold">Whether its text is bold.</param>
sealed record StretchOfTheStatusLine(string Text, string? Colour, bool IsBold)
{
   /// <summary>The stretch as the hub's answer <paramref name="stretch"/> has it.</summary>
   public static StretchOfTheStatusLine InThe(StyledStretchInTheAnswer stretch) => new(stretch.Text, stretch.Foreground, stretch.Bold);
}
