using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Cloyd.Chat.Status;

/// <summary>
/// Text that has one style in a chat's status line, such as the weekly allowance shown as "week 100%" in bold red.
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
   /// <summary>A run with this stretch's weight and its colour from <see cref="ColoursOfTheTerminal"/>.</summary>
   public Run AsRun()
   {
      var run = new Run(Text) { FontWeight = IsBold ? FontWeight.Bold : FontWeight.Normal };
      return Colour is {} name && ColoursOfTheTerminal.Named(name) is {} colour ? colour.AsTheForegroundOf(run) : run;
   }
}
