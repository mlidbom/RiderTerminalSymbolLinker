using System.Text.RegularExpressions;
using Cloyd.Terminals;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// A program, <c>ProgramTakingTheMouse.ps1</c>, that a specification runs in a session's shell: it takes the mouse as
/// Codex does on Windows, and then turns off urxvt's mouse encoding as crossterm does outside Windows, which conhost
/// passes on to the terminal. It says what came to it, keys and the mouse's events, until <c>q</c> is typed.
/// </summary>
static partial class ProgramTakingTheMouse
{
   /// <summary>What it says once it has the mouse, and has turned urxvt's encoding off.</summary>
   public const string SaysItHasTheMouse = "It has the mouse.";

   /// <summary>What the shell runs it with.</summary>
   public static string CommandLine => $"& '{Path.Combine(AppContext.BaseDirectory, "TheSandbox", "ProgramTakingTheMouse.ps1")}'";

   /// <summary>What came to it, as <paramref name="terminal"/> shows it once <c>q</c> ended it: null until then. Read on the UI thread.</summary>
   public static WhatCame? WhatCameToItShownIn(TerminalOfASessionHost terminal) =>
      WhatItSaysOnceEnded().Match(terminal.TextShown) is { Success: true } said
         ? new WhatCame(said.Groups["keys"].Value, int.Parse(said.Groups["ofTheMouse"].Value))
         : null;

   [GeneratedRegex(@"What came: keys \[(?<keys>.*)\], mouse events (?<ofTheMouse>\d+)")]
   private static partial Regex WhatItSaysOnceEnded();

   /// <summary>The characters of the <paramref name="Keys"/> that came to it, and how many events of the mouse.</summary>
   public sealed record WhatCame(string Keys, int EventsOfTheMouse);
}
