using System.Diagnostics;
using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract.Sandboxes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The sandbox's own hub: the Claude tooling's hub, ClaudeTooling's resident server, serving the sandbox's Claude Code
///   folder to the sandbox Rider's Claude Sessions, as the published hub serves Magnus's to his IDEs. The tooling's
///   <c>hub sandbox start</c> and <c>hub sandbox stop</c> run it, in <paramref name="folder"/>, from a copy of the
///   published build, whose stand-in for Claude Code the sandbox's sessions run.
/// </summary>
/// <remarks>
///   The hub's contract, <c>~/.claude/dev_docs/the-hubs-local-api.md</c>, says how in "A sandbox's hub". The hub stops
///   once it has been unused for 5 minutes: Claude Sessions' streams keep it in use while the sandbox Rider runs.
/// </remarks>
public sealed class SandboxHub(string folder)
{
   /// <summary>A start takes the 30 s the hub has to announce itself at most, and copies the build when it is new.</summary>
   static readonly TimeSpan HowLongACommandMayTake = TimeSpan.FromSeconds(60);

   /// <summary>The published client, which runs the tooling's commands.</summary>
   static string PublishedClient => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin", "ClaudeTooling.exe");

   /// <summary>
   ///   Has the hub run, on the published build, and returns where its parts are once it has announced its local API: a
   ///   hub that runs already, of that build, and started with the same values of the variables it serves by, runs on;
   ///   another is ended and started anew, its session hosts running on. It starts in this process's environment, which
   ///   must name the sandbox's IDEs and Royder, as <see cref="SandboxRider"/> has it: the hub sets the rest.
   /// </summary>
   public SandboxHubStarted Start() => JsonSerializer.Deserialize<SandboxHubStarted>(Run("start"))!;

   /// <summary>
   ///   Ends the hub's session hosts, as closing their tabs does, and every process that runs from the copy of the build:
   ///   the hub, its session hosts with what runs in them, and its stand-ins. Returns what it ended, as the command says it.
   /// </summary>
   public string Stop() => Run("stop").Trim();

   /// <summary>Runs <c>hub sandbox <paramref name="command"/></c> on the folder, and returns what it prints.</summary>
   /// <remarks>
   ///   The hub is started detached, with none of the client's handles, so the client's output ends as it exits.
   /// </remarks>
   string Run(string command)
   {
      var start = new ProcessStartInfo(PublishedClient, ["hub", "sandbox", command, folder]) { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
      using var client = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {start.FileName}.");
      var output = client.StandardOutput.ReadToEndAsync();
      var errors = client.StandardError.ReadToEndAsync();
      if(!client.WaitForExit(HowLongACommandMayTake))
      {
         client.Kill(); // Each later start would leave another, as on 2026-11-20, when no new console could start.
         throw new TimeoutException($"{start.FileName} hub sandbox {command} {folder} didn't exit within {HowLongACommandMayTake}.");
      }

      if(client.ExitCode != 0) throw new InvalidOperationException($"{start.FileName} hub sandbox {command} {folder} exited with {client.ExitCode}: {errors.Result}{output.Result}");
      return output.Result;
   }
}
