using Cloyd.Terminals;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The input box of the tooling owner's stand-in for Claude Code, which the sandbox's session hosts run, as a terminal
/// shows it: drawn as Claude Code draws its own, between two rules, its first line starting with the prompt sign.
/// </summary>
static class InputBoxOfTheStandInForClaudeCode
{
   /// <summary>
   /// The box's lines as <paramref name="terminal"/> shows them now: from the line with its prompt sign to the line before
   /// its closing rule, each trimmed at its end. Nothing while it shows no box. Read on the UI thread.
   /// </summary>
   public static string LinesShownIn(TerminalOfASessionHost terminal)
   {
      var lines = terminal.TextShown.Split('\n').Select(it => it.TrimEnd()).ToList();
      var first = lines.FindLastIndex(it => it.StartsWith('❯'));
      return first < 0 ? "" : string.Join("\n", lines.Skip(first).TakeWhile(it => !it.StartsWith('─')));
   }
}
