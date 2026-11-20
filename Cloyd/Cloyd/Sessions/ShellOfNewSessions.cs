namespace Cloyd.Sessions;

/// <summary>
/// The shell a <see cref="Projects.ProjectWindow"/> starts each new session's host with, which the hub then types
/// <c>claude</c> into, and the variables the shell gets on top of those Windows gives a new process.
/// </summary>
/// <param name="CommandLine">The shell's command line, such as <c>pwsh.exe</c>.</param>
/// <param name="Environment">The variables the shell gets on top of the environment Windows gives a process started afresh.</param>
sealed record ShellOfNewSessions(string CommandLine, IReadOnlyDictionary<string, string> Environment)
{
   /// <summary>
   /// The terminal's kind, as a terminal tells the programs in it what it draws: xterm's escape sequences in 256 colours,
   /// and 24-bit colour, as Iciclecreek's terminal draws them.
   /// </summary>
   static readonly IReadOnlyDictionary<string, string> WhatTheTerminalDraws = new Dictionary<string, string> { ["TERM"] = "xterm-256color", ["COLORTERM"] = "truecolor" };

   /// <summary>PowerShell 7, <c>pwsh.exe</c>, where this machine's <c>PATH</c> has it, else Windows PowerShell.</summary>
   public static ShellOfNewSessions OnThisMachine() => new(PowerShell7IsOnThePath ? "pwsh.exe" : "powershell.exe", WhatTheTerminalDraws);

   /// <summary>The same shell, with <paramref name="variable"/> set to <paramref name="value"/> as well.</summary>
   public ShellOfNewSessions With(string variable, string value) => this with { Environment = new Dictionary<string, string>(Environment) { [variable] = value } };

   static bool PowerShell7IsOnThePath =>
      (System.Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                                                                .Any(folder => File.Exists(Path.Combine(folder, "pwsh.exe")));
}
