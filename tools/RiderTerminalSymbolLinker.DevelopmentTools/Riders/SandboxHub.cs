using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

   const string HubExecutable = "ClaudeTooling.Hub.exe";

   static readonly TimeSpan HowLongStartingMayTake = TimeSpan.FromSeconds(30);

   static string PublishedFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin");

   string CopyFolder => Path.Combine(folder, "bin");

   string Hub => Path.Combine(CopyFolder, HubExecutable);

   /// <summary>Where the copy records the last write time of the published hub it was copied from, in ticks.</summary>
   string CopiedFromFile => Path.Combine(folder, "copied-from-build.txt");

   /// <summary>
   ///   The variables of its start environment that the hub serves the sandbox by: its Claude Code folder and the folder
   ///   it announces in, as its contract has it; for its Rider restart, the folder of the IDEs' announcements and
   ///   Royder's port, which a hub outside <c>~/.claude/bin</c> takes from its start environment; and its
   ///   <see cref="Log"/>.
   /// </summary>
   static readonly string[] TheVariablesItServesBy =
   [
      "CLAUDE_CONFIG_DIR", "CLAUDE_TOOLING_LOCAL_API_FOLDER", "CLAUDE_SESSIONS_LOCAL_API_FOLDER", "DOTNET_ANALYSIS_AND_REFACTORING_MCP_PORT",
      "CLAUDE_TOOLING_HUB_LOG"
   ];

   /// <summary>
   ///   The hub's log, which <c>CLAUDE_TOOLING_HUB_LOG</c> names for it: without it, it would write to the machine's,
   ///   <c>~/.claude/hub.log</c>, among the lines of the hub Magnus's sessions use.
   /// </summary>
   public string Log => Path.Combine(folder, "hub.log");

   /// <summary>
   ///   Where the copy records the hub it started, by its process, and <see cref="TheVariablesItServesBy"/> as it started
   ///   it: a hub that runs with others, as after a change to the tools, is ended and started anew.
   /// </summary>
   string StartedFile => Path.Combine(folder, "started.json");

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
   ///   shell, so that it holds none of this process's handles. A hub that runs in another environment is ended first.
   /// </summary>
   public void Run()
   {
      TakeThePublishedBuild();
      EndTheHubIfItRunsInAnotherEnvironment();
      var start = new ProcessStartInfo(Path.Combine(CopyFolder, "ClaudeTooling.exe")) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
      start.ArgumentList.Add("hub");
      start.ArgumentList.Add("start");
      using(var client = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {start.FileName}."))
      {
         if(!client.WaitForExit(HowLongStartingMayTake))
         {
            client.Kill(); // Each later start would leave another, as on 2026-11-20, when no new console could start.
            throw new TimeoutException($"{start.FileName} hub start didn't exit within {HowLongStartingMayTake}.");
         }
         if(client.ExitCode != 0) throw new InvalidOperationException($"{start.FileName} hub start exited with {client.ExitCode}.");
      }

      var clock = Stopwatch.StartNew();
      while(!AnnouncesTheBuildCopied())
      {
         if(clock.Elapsed > HowLongStartingMayTake)
            throw new TimeoutException($"Gave up after {HowLongStartingMayTake} waiting for the sandbox's hub to announce its local API in {AnnouncementFile}. Its log is {Log}.");
         Thread.Sleep(100);
      }

      File.WriteAllText(StartedFile, new JsonObject { ["processId"] = RunningProcessId(), ["environment"] = TheEnvironmentItStartsIn() }.ToJsonString());
   }

   /// <summary>
   ///   Asks the hub to restart the sandbox Rider, through its local API's <c>POST /rider-restart</c>, as Claude Sessions
   ///   asks for a button Magnus presses himself: <c>askedByMagnus</c>, so the hub shows him no prompt, whatever has the
   ///   input focus. Returns once the hub has started the restart; <see cref="TheRiderRestart"/> tells how it goes.
   /// </summary>
   /// <remarks>
   ///   Not as an agent asks, with <c>ClaudeTooling.exe rider restart</c>: that shows Magnus the prompt while a window of
   ///   the sandbox Rider has the input focus, as it has once it has started.
   /// </remarks>
   public void AskToRestartRider(string why, string askedBy)
   {
      using var client = ClientOfTheHub() ?? throw new InvalidOperationException("The sandbox's hub doesn't run.");
      using var answer = client.PostAsync("/rider-restart", JsonContent.Create(new { why, askedBy, askedByMagnus = true })).Result;
      if(answer.StatusCode != HttpStatusCode.Accepted)
         throw new InvalidOperationException($"The sandbox's hub answered the request to restart Rider with {(int)answer.StatusCode}: {answer.Content.ReadAsStringAsync().Result}");
   }

   /// <summary>
   ///   The Rider restart under way, else the last one since the hub started, else null, as the hub's
   ///   <c>GET /rider-restart</c> tells of it: its <c>stage</c>, <c>done</c> at the end, the <c>progress</c> of each step,
   ///   and its <c>outcome</c>.
   /// </summary>
   public JsonNode? TheRiderRestart()
   {
      using var client = ClientOfTheHub() ?? throw new InvalidOperationException("The sandbox's hub doesn't run.");
      return JsonNode.Parse(client.GetStringAsync("/rider-restart").Result)!["restart"];
   }

   /// <summary>
   ///   Ends the hub that runs, if the copy didn't start it in the environment this process would start it in, or doesn't
   ///   know: started with other <see cref="TheVariablesItServesBy"/>, it would serve the sandbox wrongly, and a hub
   ///   outside <c>~/.claude/bin</c> with no folder of IDEs only rehearses a Rider restart, showing Magnus its prompt.
   /// </summary>
   void EndTheHubIfItRunsInAnotherEnvironment()
   {
      if(RunningProcessId() is not {} running) return;
      var started = File.Exists(StartedFile) ? JsonNode.Parse(File.ReadAllText(StartedFile)) : null;
      if(started?["processId"]?.GetValue<int>() == running && JsonNode.DeepEquals(started["environment"], TheEnvironmentItStartsIn())) return;

      using var hub = Process.GetProcessById(running);
      if(!hub.MainModule!.FileName.StartsWith(CopyFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
         throw new InvalidOperationException($"The sandbox hub's announcement names process {running}, {hub.MainModule.FileName}, which isn't the sandbox's copy of the hub.");
      hub.Kill();
      hub.WaitForExit();
   }

   /// <summary>The process of the hub the announcement names, if it runs.</summary>
   int? RunningProcessId() => Announcement()?["processId"]!.GetValue<int>() is {} processId && IsRunning(processId) ? processId : null;

   static JsonObject TheEnvironmentItStartsIn() => new(TheVariablesItServesBy.Select(it => KeyValuePair.Create(it, (JsonNode?)Environment.GetEnvironmentVariable(it))));

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
         var id = host!["id"]!.GetValue<string>();
         using var answer = client.PostAsync($"/session-hosts/{id}/end", content: null).Result;
         if(answer.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound) continue;
         throw new InvalidOperationException($"The sandbox's hub answered ending the session host {id} with {(int)answer.StatusCode}: {answer.Content.ReadAsStringAsync().Result}");
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
