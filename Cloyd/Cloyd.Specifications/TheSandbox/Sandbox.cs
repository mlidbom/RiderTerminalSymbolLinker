using Cloyd.Sessions;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The machine as the specifications see it: a <see cref="SandboxHub"/> of their own, whose sessions run the tooling
/// owner's stand-in for Claude Code. So a specification starts real session hosts, with a Claude Code in each, in
/// seconds and at no cost, and never touches a session of Magnus's. Started once, as the first specification needs it.
/// </summary>
/// <remarks>
/// The stand-in (<c>~/.claude/src/StandInForClaudeCode</c>) runs a session as Claude Code does: it keeps its record and
/// transcript where <c>CLAUDE_CONFIG_DIR</c> says, which the hub reads, and draws an input box as Claude Code does. It
/// says <c>New session &lt;id&gt;</c> as it starts, and the size of its terminal.
/// </remarks>
sealed class Sandbox
{
   static readonly Lazy<Sandbox> StartedOnce = new(() => new Sandbox());

   readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloydSpecifications");

   Sandbox()
   {
      HubOfTheSandbox = new SandboxHub(Path.Combine(_root, "hub"));
      TakeTheStandInForClaudeCode();
      HubOfTheSandbox.Run();
      ShellOfItsSessions = ShellOfNewSessions.OnThisMachine() with { CommandLine = "pwsh.exe -NoLogo -NoProfile" };
      ShellOfItsSessions = ShellOfItsSessions.With("PATH", StandInFolder + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
   }

   public static Sandbox Started => StartedOnce.Value;

   public SandboxHub HubOfTheSandbox { get; }

   /// <summary>
   /// The shell the sessions of the specifications' project windows start in: PowerShell 7 without Magnus's profile, so
   /// that it starts the same anywhere, with the stand-in for Claude Code first on its <c>PATH</c>.
   /// </summary>
   public ShellOfNewSessions ShellOfItsSessions { get; }

   /// <summary>The folder of the specifications' projects, each in a folder of its own.</summary>
   public string ProjectsFolder => Path.Combine(_root, "projects");

   /// <summary>Where the tooling owner publishes the stand-in for Claude Code, a <c>claude.exe</c> with its files.</summary>
   static string StandInBuild => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin", "stand-in-for-claude-code");

   /// <summary>
   /// The sandbox's copy of the <see cref="StandInBuild"/>: a copy, so that the stand-ins that run in session hosts left
   /// from an earlier run hold no file of the build the tooling owner republishes.
   /// </summary>
   string StandInFolder => Path.Combine(_root, "stand-in-for-claude-code");

   /// <summary>Copies the <see cref="StandInBuild"/>'s files that are newer than the copy's, renaming those that a running stand-in holds out of the way.</summary>
   void TakeTheStandInForClaudeCode()
   {
      Directory.CreateDirectory(StandInFolder);
      foreach(var built in Directory.GetFiles(StandInBuild))
      {
         var copy = Path.Combine(StandInFolder, Path.GetFileName(built));
         if(File.Exists(copy) && File.GetLastWriteTimeUtc(copy) >= File.GetLastWriteTimeUtc(built)) continue;
         if(File.Exists(copy)) File.Move(copy, $"{copy}.replaced-{Guid.NewGuid():N}");
         File.Copy(built, copy);
      }
   }
}
