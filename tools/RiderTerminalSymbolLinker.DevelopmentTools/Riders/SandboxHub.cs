using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The sandbox's own hub: the Claude tooling's hub, ClaudeTooling's resident server, serving the sandbox's Claude Code
///   folder to the sandbox Rider's Claude Sessions, as the published hub serves Magnus's to his IDEs. A copy of the
///   published hub's build, from <c>~/.claude/bin</c>, so that the sandbox tries the plugin with the hub Magnus's IDEs
///   talk to. Its session hosts, in which the sandbox's tabs run their sessions, are started from the copy too.
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
   /// <summary>
   ///   The executables the hub is: the hub itself, its client, which starts the hub when none runs, and its session host,
   ///   which the hub starts from beside itself, and whose attach client the sandbox's tabs run.
   /// </summary>
   static string[] Executables => [HubExecutable, "ClaudeTooling.exe", "ClaudeTooling.SessionHost.exe"];

   /// <summary>
   ///   The hub's own executable, as the published build names it: <c>ClaudeTooling.Hub.exe</c> since the tooling owner
   ///   renamed the resident server the hub (2026-11-19), and <c>ClaudeTooling.Server.exe</c> before.
   /// </summary>
   static string HubExecutable => File.Exists(Path.Combine(PublishedFolder, "ClaudeTooling.Hub.exe")) ? "ClaudeTooling.Hub.exe" : FormerHubExecutable;

   const string FormerHubExecutable = "ClaudeTooling.Server.exe";

   static readonly TimeSpan HowLongStartingMayTake = TimeSpan.FromSeconds(30);

   static string PublishedFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin");

   string CopyFolder => Path.Combine(folder, "bin");

   string Hub => Path.Combine(CopyFolder, HubExecutable);

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

   /// <summary>
   ///   The session hosts of the sandbox's Claude Code folder that run, as the hub's <c>GET /session-hosts</c> lists them:
   ///   none when no hub runs, as none would have started one.
   /// </summary>
   public JsonArray SessionHosts()
   {
      using var client = ClientOfTheHub();
      return client is null ? [] : JsonNode.Parse(client.GetStringAsync("/session-hosts").Result)!["sessionHosts"]!.AsArray();
   }

   /// <summary>
   ///   Ends every session host of the sandbox's Claude Code folder, as closing its tab does, and returns how many it
   ///   ended: they outlive the sandbox Rider, as the sessions of Magnus's IDEs outlive his, and would run on after the
   ///   sandbox stops.
   /// </summary>
   public int EndItsSessionHosts()
   {
      using var client = ClientOfTheHub();
      if(client is null) return 0;
      var hosts = JsonNode.Parse(client.GetStringAsync("/session-hosts").Result)!["sessionHosts"]!.AsArray();
      foreach(var host in hosts)
      {
         using var answer = client.PostAsync($"/session-hosts/{host!["id"]!.GetValue<string>()}/end", content: null).Result;
         if(answer.StatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.NotFound))
            throw new InvalidOperationException($"The sandbox's hub answered ending the session host {host["id"]} with {(int)answer.StatusCode}: {answer.Content.ReadAsStringAsync().Result}");
      }

      return hosts.Count;
   }

   /// <summary>A client of the hub's local API, with its token, or null when no hub runs.</summary>
   HttpClient? ClientOfTheHub()
   {
      if(Announcement() is not {} announcement || !IsRunning(announcement["processId"]!.GetValue<int>())) return null;
      var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{announcement["port"]!.GetValue<int>()}"), Timeout = TimeSpan.FromSeconds(30) };
      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", announcement["token"]!.GetValue<string>());
      return client;
   }

   /// <summary>Whether the announcement names a hub that runs, of the build copied.</summary>
   bool AnnouncesTheBuildCopied()
   {
      if(Announcement() is not {} announcement) return false;
      var build = DateTime.Parse(announcement["build"]!.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
      return build == File.GetLastWriteTimeUtc(Hub) && IsRunning(announcement["processId"]!.GetValue<int>());
   }

   static bool IsRunning(int processId) => Process.GetProcesses().Any(it => it.Id == processId);

   /// <summary>Copies the published hub's build, unless the copy is of it already.</summary>
   void TakeThePublishedBuild()
   {
      var published = File.GetLastWriteTimeUtc(Path.Combine(PublishedFolder, HubExecutable)).Ticks.ToString(CultureInfo.InvariantCulture);
      if(File.Exists(Hub) && File.Exists(CopiedFromFile) && File.ReadAllText(CopiedFromFile) == published) return;

      Directory.CreateDirectory(CopyFolder);
      var replacedAt = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
      foreach(var executable in Executables)
      {
         var copy = Path.Combine(CopyFolder, executable);
         if(File.Exists(copy)) File.Move(copy, $"{copy}.replaced-{replacedAt}"); // A running executable can be renamed, not replaced.
         File.Copy(Path.Combine(PublishedFolder, executable), copy);
      }

      // The hub's former executable goes too, once the build names it anew.
      var former = Path.Combine(CopyFolder, FormerHubExecutable);
      if(HubExecutable != FormerHubExecutable && File.Exists(former)) File.Move(former, $"{former}.replaced-{replacedAt}");
      File.SetLastWriteTimeUtc(Hub, DateTime.UtcNow); // A build of its own, whose pipe is its own.
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
