using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.Sandboxes;
using Cloyd.ClaudeCode;
using Cloyd.Hub;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// A <see cref="Sandbox"/>'s own hub, apart from the published one: <c>ClaudeTooling.exe hub sandbox start</c>'s, from a
/// copy of the published build, serving a Claude Code folder of its own and announcing itself in a folder of its own.
/// <see cref="Stop"/> ends it, with its session hosts and what runs in them.
/// </summary>
/// <remarks>
/// See <c>~/.claude/dev_docs/the-hubs-local-api.md</c>, "A sandbox's hub".
/// </remarks>
sealed class SandboxHub
{
   static readonly TimeSpan HowLongACommandMayTake = TimeSpan.FromSeconds(40);

   static readonly string Client = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".claude", "bin", "ClaudeTooling.exe");

   readonly SandboxHubStarted _started;

   SandboxHub(SandboxHubStarted started) => _started = started;

   /// <summary>
   /// A new hub, of the folder <paramref name="folder"/>, once it runs. It stops itself, as <see cref="StopIn"/> stops it,
   /// once the specifications' process has exited, however it ended: so a run that is killed, as NCrunch ends the
   /// processes it pools, leaves no hub running.
   /// </summary>
   public static SandboxHub RunIn(string folder) =>
      new(JsonSerializer.Deserialize<SandboxHubStarted>(Printed("hub", "sandbox", "start", folder, "--ending-with", System.Environment.ProcessId.ToString()), LocalApiProtocol.Json)!);

   /// <summary>
   /// Stops the hub of the folder <paramref name="folder"/>: its session hosts end, as closing their tabs ends them, then
   /// every process that runs from its copy of the build, what runs in its hosts included. The folder can be deleted then.
   /// </summary>
   public static void StopIn(string folder) => Printed("hub", "sandbox", "stop", folder);

   /// <summary>Stops the hub, as <see cref="StopIn"/> does.</summary>
   public void Stop() => StopIn(_started.Folder);

   public TheHub Hub => TheHub.AnnouncedIn(new DirectoryInfo(_started.AnnouncementFolder));

   /// <summary>
   /// The sessions the hub has transcripts of, as it lists them now: asked on the thread pool, so that it may be asked on
   /// the UI thread too, as a condition of <see cref="SpecificationOpeningWindows.WaitUntil"/> is.
   /// </summary>
   public IReadOnlyList<SessionOnDisk> SessionsOnDisk() => Task.Run(() => Hub.SessionsOnDisk()).GetAwaiter().GetResult();

   /// <summary>The Claude Codes that run, as the hub lists them now: asked as <see cref="SessionsOnDisk"/> is.</summary>
   public IReadOnlyList<RunningClaudeCode> RunningClaudeCodes() => [.. Task.Run(() => Hub.RunningSessions()).GetAwaiter().GetResult().OfType<RunningClaudeCode>()];

   /// <summary>
   /// The transcript of <paramref name="session"/>, which ran in <paramref name="directory"/>, in the Claude Code folder the
   /// hub serves: <c>projects/&lt;the directory, each character but a letter or digit a dash&gt;/&lt;the session&gt;.jsonl</c>,
   /// as Claude Code keeps it.
   /// </summary>
   public FileInfo TranscriptOf(ClaudeCodeSessionId session, string directory) =>
      new(Path.Combine(_started.ClaudeCodeFolder, "projects", Regex.Replace(directory, "[^A-Za-z0-9]", "-"), $"{session}.jsonl"));

   /// <summary>Deletes the transcript of <paramref name="session"/> from the Claude Code folder the hub serves, as archiving or deleting the session does.</summary>
   public void DeleteTheTranscriptOf(ClaudeCodeSessionId session)
   {
      foreach(var transcript in Directory.EnumerateFiles(Path.Combine(_started.ClaudeCodeFolder, "projects"), $"{session}.jsonl", SearchOption.AllDirectories))
         File.Delete(transcript);
   }

   /// <summary>
   /// The variables a process of the sandbox takes, on top of the environment it runs in, such as a <c>Cloyd.exe</c> a
   /// specification runs, so that it reaches this hub: <c>CLAUDE_CONFIG_DIR</c>, <c>CLAUDE_TOOLING_LOCAL_API_FOLDER</c> and
   /// <c>CLAUDE_TOOLING_HUB_LOG</c>.
   /// </summary>
   public IReadOnlyDictionary<string, string> Environment => _started.Environment;

   /// <summary>The folder of the sandbox's copy of the stand-in for Claude Code, a <c>claude.exe</c>: first on the <c>PATH</c> of the sessions' shells, so that <c>claude</c> there runs it.</summary>
   public string FolderOfTheStandInForClaudeCode => _started.FolderOfTheStandInForClaudeCode;

   /// <summary>
   /// The folder of the hub's stand-in for Codex, a <c>codex.exe</c>: first on the <c>PATH</c> of the sessions' shells, so
   /// that <c>codex</c> there runs it, over npm's <c>codex.ps1</c>, and nothing of a scenario reaches OpenAI.
   /// </summary>
   public string FolderOfTheStandInForCodex => _started.FolderOfTheStandInForCodex;

   /// <summary>
   /// The Codex home the hub serves, which <c>CODEX_HOME</c> names in its <see cref="Environment"/>: where the stand-in for
   /// Codex keeps the sandbox's Codex sessions (<see cref="CodexSessionOfASpecification"/>). Outside the sandbox's folder,
   /// in a short folder of the hub's, for the socket of Codex's daemon, whose path may hold 107 characters at most.
   /// </summary>
   public string CodexFolder => _started.CodexFolder;

   /// <summary>What the published client prints, run with <paramref name="arguments"/>: an exception when it fails.</summary>
   static string Printed(params string[] arguments)
   {
      var run = new ProcessStartInfo(Client) { RedirectStandardOutput = true, RedirectStandardError = true };
      foreach(var argument in arguments) run.ArgumentList.Add(argument);
      using var command = Process.Start(run) ?? throw new InvalidOperationException($"Could not start {Client}.");
      var printed = command.StandardOutput.ReadToEndAsync();
      var failure = command.StandardError.ReadToEndAsync();
      if(!command.WaitForExit(HowLongACommandMayTake))
      {
         command.Kill();
         throw new TimeoutException($"{Client} {string.Join(' ', arguments)} didn't exit within {HowLongACommandMayTake}.");
      }

      if(command.ExitCode != 0) throw new InvalidOperationException($"{Client} {string.Join(' ', arguments)} exited with {command.ExitCode}: {failure.Result}");
      return printed.Result;
   }
}
