using System.Diagnostics;
using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.Sandboxes;
using Cloyd.Hub;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The specifications' own hub, apart from the published one: <c>ClaudeTooling.exe hub sandbox start</c>'s, from a copy
/// of the published build, serving a Claude Code folder of its own and announcing itself in a folder of its own. Its
/// session hosts outlive the specifications' run, as Magnus's outlive his IDEs.
/// </summary>
/// <remarks>
/// It stops by itself once it has been unused for 5 minutes. See <c>~/.claude/dev_docs/the-hubs-local-api.md</c>,
/// "A sandbox's hub".
/// </remarks>
sealed class SandboxHub
{
   static readonly TimeSpan HowLongStartingMayTake = TimeSpan.FromSeconds(40);

   readonly SandboxHubStarted _started;

   SandboxHub(SandboxHubStarted started) => _started = started;

   /// <summary>
   /// The hub of <paramref name="folder"/>, once it runs on the published build: the one that runs already, when it runs
   /// that build, else a new one.
   /// </summary>
   public static SandboxHub RunIn(string folder)
   {
      var client = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin", "ClaudeTooling.exe");
      var start = new ProcessStartInfo(client) { RedirectStandardOutput = true, RedirectStandardError = true, ArgumentList = { "hub", "sandbox", "start", folder } };
      using var command = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {client}.");
      var printed = command.StandardOutput.ReadToEndAsync();
      var failure = command.StandardError.ReadToEndAsync();
      if(!command.WaitForExit(HowLongStartingMayTake))
      {
         command.Kill();
         throw new TimeoutException($"{client} hub sandbox start {folder} didn't exit within {HowLongStartingMayTake}.");
      }
      if(command.ExitCode != 0) throw new InvalidOperationException($"{client} hub sandbox start {folder} exited with {command.ExitCode}: {failure.Result}");

      return new(JsonSerializer.Deserialize<SandboxHubStarted>(printed.Result, LocalApiProtocol.Json)!);
   }

   public TheHub Hub => TheHub.AnnouncedIn(new DirectoryInfo(_started.AnnouncementFolder));

   /// <summary>The Claude Code folder the hub serves: its sessions keep their records and transcripts there.</summary>
   public string ClaudeCodeFolder => _started.ClaudeCodeFolder;

   /// <summary>The folder of the sandbox's copy of the stand-in for Claude Code, a <c>claude.exe</c>: first on the <c>PATH</c> of the sessions' shells, so that <c>claude</c> there runs it.</summary>
   public string FolderOfTheStandInForClaudeCode => _started.FolderOfTheStandInForClaudeCode;
}
