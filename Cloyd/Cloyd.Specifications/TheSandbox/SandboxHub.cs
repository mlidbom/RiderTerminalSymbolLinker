using System.Diagnostics;
using System.Globalization;
using Cloyd.Hub;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The specifications' own hub: a copy of the published hub's build, from <c>~/.claude/bin</c>, serving a Claude Code
/// folder of its own and announcing itself in a folder of its own, as the hub's contract says a sandbox runs one. Its
/// session hosts are started from the copy too, and outlive the specifications' run, as Magnus's outlive his IDEs.
/// </summary>
/// <remarks>
/// The hub's pipe is named after the last write time of its executable, so the copy is given a write time of its own:
/// with the published one's, the copy's client would hand its commands to the published hub, which serves Magnus's
/// Claude Code folder. It stops by itself once it has been unused for 5 minutes.
/// </remarks>
/// <remarks>
/// refactor: <c>tools/RiderTerminalSymbolLinker.DevelopmentTools/Riders/SandboxHub.cs</c> copies and starts the published
/// hub the same way, for the plugins' sandbox Rider. A hub of one's own for trying things belongs to the tooling, as a
/// command of <c>ClaudeTooling.exe</c> that both would run: asked of the tooling owner on 2026-11-20.
/// </remarks>
sealed class SandboxHub
{
   const string HubExecutable = "ClaudeTooling.Hub.exe", ClientExecutable = "ClaudeTooling.exe", SessionHostExecutable = "ClaudeTooling.SessionHost.exe";

   static readonly TimeSpan HowLongStartingMayTake = TimeSpan.FromSeconds(30);

   readonly string _folder;

   public SandboxHub(string folder) => _folder = folder;

   static string PublishedFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin");

   string CopyFolder => Path.Combine(_folder, "bin");

   /// <summary>Where the copy records the last write time of the published hub it was copied from, in ticks.</summary>
   string CopiedFromFile => Path.Combine(_folder, "copied-from-build.txt");

   /// <summary>The folder the hub announces its local API in, which <c>CLAUDE_TOOLING_LOCAL_API_FOLDER</c> names for it.</summary>
   DirectoryInfo AnnouncementFolder => new(Path.Combine(_folder, "local-api"));

   /// <summary>The Claude Code folder the hub serves, which <c>CLAUDE_CONFIG_DIR</c> names for it and for the shells of its session hosts.</summary>
   public string ClaudeCodeFolder => Path.Combine(_folder, "claude-code");

   /// <summary>The hub's log, which <c>CLAUDE_TOOLING_HUB_LOG</c> names for it: else it would write among the lines of Magnus's hub.</summary>
   string Log => Path.Combine(_folder, "hub.log");

   public TheHub Hub => TheHub.AnnouncedIn(AnnouncementFolder);

   /// <summary>
   /// Has the hub run, on the published build, and returns once it has announced its local API. It is started with no
   /// window, through the shell, so that it holds none of this process's handles, in an environment that names its
   /// folders.
   /// </summary>
   public void Run()
   {
      TakeThePublishedBuild();
      ForgetTheSessionsWhoseClaudeCodeIsGone();
      Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", ClaudeCodeFolder);
      Environment.SetEnvironmentVariable("CLAUDE_TOOLING_LOCAL_API_FOLDER", AnnouncementFolder.FullName);
      Environment.SetEnvironmentVariable("CLAUDE_TOOLING_HUB_LOG", Log);
      var start = new ProcessStartInfo(Path.Combine(CopyFolder, ClientExecutable)) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden, ArgumentList = { "hub", "start" } };
      using(var client = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {start.FileName}."))
      {
         if(!client.WaitForExit(HowLongStartingMayTake))
         {
            client.Kill();
            throw new TimeoutException($"{start.FileName} hub start didn't exit within {HowLongStartingMayTake}.");
         }
         if(client.ExitCode != 0) throw new InvalidOperationException($"{start.FileName} hub start exited with {client.ExitCode}. Its log is {Log}.");
      }

      var clock = Stopwatch.StartNew();
      while(AnnouncementOfTheHub.In(AnnouncementFolder) is null)
      {
         if(clock.Elapsed > HowLongStartingMayTake) throw new TimeoutException($"Gave up after {HowLongStartingMayTake} waiting for the sandbox's hub to announce itself in {AnnouncementFolder}. Its log is {Log}.");
         Thread.Sleep(100);
      }
   }

   /// <summary>
   /// Deletes the records of running sessions, <c>sessions/&lt;process id&gt;.json</c> in the Claude Code folder, whose
   /// process no longer runs: the stand-ins that earlier runs killed with their hosts, hundreds of them, which the hub
   /// would read on every reading. Claude Code deletes its record only as it exits.
   /// </summary>
   void ForgetTheSessionsWhoseClaudeCodeIsGone()
   {
      var records = new DirectoryInfo(Path.Combine(ClaudeCodeFolder, "sessions"));
      if(!records.Exists) return;
      foreach(var record in records.EnumerateFiles("*.json"))
         if(int.TryParse(Path.GetFileNameWithoutExtension(record.Name), out var processId) && !ProcessRuns(processId))
            record.Delete();

      static bool ProcessRuns(int processId)
      {
         try
         {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
         }
         catch(ArgumentException)
         {
            return false; // No process has the id.
         }
      }
   }

   /// <summary>Copies the published hub's build, unless the copy is of it already.</summary>
   void TakeThePublishedBuild()
   {
      var published = File.GetLastWriteTimeUtc(Path.Combine(PublishedFolder, HubExecutable)).Ticks.ToString(CultureInfo.InvariantCulture);
      var hub = Path.Combine(CopyFolder, HubExecutable);
      if(File.Exists(hub) && File.Exists(CopiedFromFile) && File.ReadAllText(CopiedFromFile) == published) return;

      EndTheHubThatRuns();
      Directory.CreateDirectory(CopyFolder);
      var replacedAt = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
      foreach(var executable in new[] { HubExecutable, ClientExecutable, SessionHostExecutable })
      {
         var copy = Path.Combine(CopyFolder, executable);
         if(File.Exists(copy)) File.Move(copy, $"{copy}.replaced-{replacedAt}"); // A running session host's executable can be renamed, not replaced.
         File.Copy(Path.Combine(PublishedFolder, executable), copy);
      }

      File.SetLastWriteTimeUtc(hub, DateTime.UtcNow); // A build of its own, whose pipe is its own.
      File.WriteAllText(CopiedFromFile, published);
   }

   /// <summary>Ends the hub of an earlier copy, if it runs: its session hosts run on, and the new hub finds them.</summary>
   void EndTheHubThatRuns()
   {
      if(AnnouncementOfTheHub.In(AnnouncementFolder) is null) return;

      foreach(var running in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(HubExecutable)))
         using(running)
            if(running.MainModule?.FileName.StartsWith(CopyFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) == true)
            {
               running.Kill();
               running.WaitForExit();
            }
   }
}
