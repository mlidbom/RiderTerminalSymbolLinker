using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The sandbox's own hub: the Claude tooling's hub, ClaudeTooling's resident server, serving the sandbox's Claude Code
///   folder to the sandbox Rider's Claude Sessions, as the published hub serves Magnus's to his IDEs. A copy of the
///   published hub's build, from <c>~/.claude/bin</c>, so that the sandbox tries the plugin with the hub Magnus's IDEs
///   talk to.
/// </summary>
/// <remarks>
///   The hub's contract, <c>~/.claude/dev_docs/the-hubs-local-api.md</c>, says how a sandbox runs one: started with
///   <c>CLAUDE_CONFIG_DIR</c> naming the sandbox's Claude Code folder, and <c>CLAUDE_TOOLING_LOCAL_API_FOLDER</c> a
///   folder for its announcement, in which the published hub never writes. It stops once it has been unused for 5
///   minutes: Claude Sessions' stream keeps it in use while the sandbox Rider runs.<br/>
///   The hub's pipe is named after the last write time of its executable, so the copy is given a write time of its own:
///   with the published one's, the copy's client would hand its commands to the published hub, which serves Magnus's
///   Claude Code folder. A copy of a newer published build replaces the running copy's executables as a publish does,
///   renaming them out of the way, and the running hub then hands over to the new build's.
/// </remarks>
public sealed class SandboxHub(string folder)
{
   /// <summary>The executables the hub is: its client, which starts its server when none runs, and its server.</summary>
   static readonly string[] Executables = ["ClaudeTooling.Server.exe", "ClaudeTooling.exe"];

   static readonly TimeSpan HowLongStartingMayTake = TimeSpan.FromSeconds(30);

   static string PublishedFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin");

   string CopyFolder => Path.Combine(folder, "bin");

   string Server => Path.Combine(CopyFolder, Executables[0]);

   /// <summary>Where the copy records the last write time of the published server it was copied from, in ticks.</summary>
   string CopiedFromFile => Path.Combine(folder, "copied-from-build.txt");

   /// <summary>
   ///   The folder the hub announces its local API in, which <c>CLAUDE_TOOLING_LOCAL_API_FOLDER</c> names for the hub and
   ///   for the sandbox Rider's Claude Sessions.
   /// </summary>
   public string AnnouncementFolder => Path.Combine(folder, "local-api");

   string AnnouncementFile => Path.Combine(AnnouncementFolder, "local-api.json");

   /// <summary>
   ///   Has the hub run, on the published build, and returns once it has announced its local API. It
   ///   starts in this process's environment, which must name the sandbox's Claude Code folder and
   ///   <see cref="AnnouncementFolder"/>, as <see cref="SandboxRider"/> has it, and starts with no window, through the
   ///   shell, so that it holds none of this process's handles.
   /// </summary>
   public void Run()
   {
      TakeThePublishedBuild();
      var start = new ProcessStartInfo(Path.Combine(CopyFolder, "ClaudeTooling.exe")) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
      start.ArgumentList.Add("resident-server");
      start.ArgumentList.Add("start");
      using(var client = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {start.FileName}."))
      {
         if(!client.WaitForExit(HowLongStartingMayTake)) throw new TimeoutException($"{start.FileName} resident-server start didn't exit within {HowLongStartingMayTake}.");
         if(client.ExitCode != 0) throw new InvalidOperationException($"{start.FileName} resident-server start exited with {client.ExitCode}.");
      }

      var clock = Stopwatch.StartNew();
      while(!AnnouncesTheBuildCopied())
      {
         if(clock.Elapsed > HowLongStartingMayTake)
            throw new TimeoutException($"Gave up after {HowLongStartingMayTake} waiting for the sandbox's hub to announce its local API in {AnnouncementFile}. Its log is ~/.claude/resident-server.log.");
         Thread.Sleep(100);
      }
   }

   /// <summary>What the hub's announcement says, or null while it has none.</summary>
   public JsonNode? Announcement() => File.Exists(AnnouncementFile) ? JsonNode.Parse(File.ReadAllText(AnnouncementFile)) : null;

   /// <summary>Whether the announcement names a hub that runs, of the build copied.</summary>
   bool AnnouncesTheBuildCopied()
   {
      if(Announcement() is not {} announcement) return false;
      var build = DateTime.Parse(announcement["build"]!.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
      return build == File.GetLastWriteTimeUtc(Server) && IsRunning(announcement["processId"]!.GetValue<int>());

      static bool IsRunning(int processId) => Process.GetProcesses().Any(it => it.Id == processId);
   }

   /// <summary>Copies the published hub's build, unless the copy is of it already.</summary>
   void TakeThePublishedBuild()
   {
      var published = File.GetLastWriteTimeUtc(Path.Combine(PublishedFolder, Executables[0])).Ticks.ToString(CultureInfo.InvariantCulture);
      if(File.Exists(Server) && File.Exists(CopiedFromFile) && File.ReadAllText(CopiedFromFile) == published) return;

      Directory.CreateDirectory(CopyFolder);
      var replacedAt = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
      foreach(var executable in Executables)
      {
         var copy = Path.Combine(CopyFolder, executable);
         if(File.Exists(copy)) File.Move(copy, $"{copy}.replaced-{replacedAt}"); // A running executable can be renamed, not replaced.
         File.Copy(Path.Combine(PublishedFolder, executable), copy);
      }

      File.SetLastWriteTimeUtc(Server, DateTime.UtcNow); // A build of its own, whose pipe is its own.
      File.WriteAllText(CopiedFromFile, published);
      DeleteTheReplacedExecutablesNoLongerRunning();
   }

   void DeleteTheReplacedExecutablesNoLongerRunning()
   {
      foreach(var replaced in Directory.GetFiles(CopyFolder, "*.exe.replaced-*"))
      {
         try
         {
            File.Delete(replaced);
         }
         catch(UnauthorizedAccessException)
         {
            // Windows refuses to delete an executable a process runs, such as the hub that hands over to the new build: a
            // later copy deletes it.
         }
      }
   }
}
