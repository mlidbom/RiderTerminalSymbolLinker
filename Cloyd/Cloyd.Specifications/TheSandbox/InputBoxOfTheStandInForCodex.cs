using Cloyd.Terminals;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The input box of the stand-in for Codex, which the sandbox's session hosts run for a Codex session, as a terminal
/// shows it: drawn as Codex draws its own, its first line starting with the prompt sign, and an empty line under it.
/// </summary>
static class InputBoxOfTheStandInForCodex
{
   /// <summary>
   /// The box's lines as <paramref name="terminal"/> shows them now: from the last line with its prompt sign to the line
   /// before the empty one under the box, each trimmed, so that a line after the first reads the same indented or not.
   /// Nothing while it shows no box. Read on the UI thread.
   /// </summary>
   public static string LinesShownIn(TerminalOfASessionHost terminal)
   {
      var lines = terminal.TextShown.Split('\n').Select(it => it.Trim()).ToList();
      var first = lines.FindLastIndex(it => it.StartsWith('›'));
      return first < 0 ? "" : string.Join("\n", lines.Skip(first).TakeWhile(it => it.Length > 0));
   }
}
