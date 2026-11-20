using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;
using Cloyd.Sessions;

namespace Cloyd.Hub;

/// <summary>
/// The Claude tooling's hub, through its local API: what Cloyd asks of it about the session hosts and the Claude Codes
/// that run in them. The hub owns everything about Claude Code; its contract is
/// <c>~/.claude/dev_docs/the-hubs-local-api.md</c>.
/// </summary>
/// <remarks>
/// The hub is republished several times a day, under the running sessions, and each publish starts a new hub, on a port
/// and with a token of its own. So a request whose connection is refused, or that gets a 401, reads the
/// <see cref="AnnouncementOfTheHub"/> again and is sent again, for <see cref="HowLongANewHubMayTakeToAnswer"/>, as the
/// contract says a reader must.
/// </remarks>
sealed partial class TheHub
{
   /// <summary>The version of the hub's protocol that Cloyd is written against.</summary>
   const int ProtocolCloydSpeaks = 1;

   /// <summary>Where the published hub announces itself, unless <see cref="VariableNamingTheFolderOfTheAnnouncement"/> names another folder.</summary>
   static DirectoryInfo FolderOfThePublishedHubsAnnouncement => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeTooling"));

   /// <summary>The variable that names the folder of the announcement of a hub tried out apart from the published one, as a sandbox's.</summary>
   const string VariableNamingTheFolderOfTheAnnouncement = "CLAUDE_TOOLING_LOCAL_API_FOLDER";

   static readonly TimeSpan HowLongANewHubMayTakeToAnswer = TimeSpan.FromSeconds(30);
   static readonly TimeSpan BetweenTries = TimeSpan.FromMilliseconds(250);
   static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

   readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
   readonly DirectoryInfo _folderOfTheAnnouncement;
   readonly bool _isThePublishedHub;

   TheHub(DirectoryInfo folderOfTheAnnouncement, bool isThePublishedHub)
   {
      _folderOfTheAnnouncement = folderOfTheAnnouncement;
      _isThePublishedHub = isThePublishedHub;
   }

   /// <summary>
   /// The hub Cloyd's environment names: the one tried out apart from the published one, whose announcement's folder
   /// <c>CLAUDE_TOOLING_LOCAL_API_FOLDER</c> names, as a sandbox's; else the published hub, which Cloyd starts when none
   /// runs, as after a reboot, before any Claude Code has started one.
   /// </summary>
   public static TheHub OfCloydsEnvironment() =>
      Environment.GetEnvironmentVariable(VariableNamingTheFolderOfTheAnnouncement) is {Length: > 0} folder
         ? AnnouncedIn(new DirectoryInfo(folder))
         : new TheHub(FolderOfThePublishedHubsAnnouncement, isThePublishedHub: true);

   /// <summary>The hub that announces itself in <paramref name="folder"/>: one tried out apart from the published one, which whoever tries it starts.</summary>
   public static TheHub AnnouncedIn(DirectoryInfo folder) => new(folder, isThePublishedHub: false);

   /// <summary>The Claude Code folder the hub serves: where the sessions of its hosts keep their transcripts.</summary>
   public async Task<ClaudeCodeFolder> ClaudeCodeFolderItServes(CancellationToken cancellation = default) =>
      (await TheAnnouncement(new TimeToAnswer(_folderOfTheAnnouncement), cancellation)).ClaudeCodeFolderItServes;

   /// <summary>Every session host that runs, in the order they started.</summary>
   public async Task<IReadOnlyList<SessionHostOfTheHub>> SessionHosts(CancellationToken cancellation = default)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Get, it.AddressOf("session-hosts")), cancellation);
      return (await JsonOf(answer, cancellation))["sessionHosts"].Deserialize<List<SessionHostOfTheHub>>(Json)!;
   }

   /// <summary>
   /// Starts a session host running <paramref name="shell"/> in <paramref name="directory"/>, known by
   /// <paramref name="label"/>, and returns it once it listens. A host that didn't tell of itself in time, as on a very
   /// busy machine, the hub ends and answers 500 for: it is asked once more.
   /// </summary>
   public async Task<SessionHostOfTheHub> StartASessionHost(ShellOfNewSessions shell, string directory, string label, CancellationToken cancellation = default)
   {
      var body = new JsonObject { ["shell"] = shell.CommandLine, ["directory"] = directory, ["environment"] = new JsonObject(shell.Environment.Select(it => KeyValuePair.Create(it.Key, (JsonNode?)it.Value))), ["label"] = label };
      var answer = await Send(Request, cancellation);
      if(answer.StatusCode == HttpStatusCode.InternalServerError)
      {
         answer.Dispose();
         answer = await Send(Request, cancellation);
      }

      using(answer) return (await JsonOf(answer, cancellation))["sessionHost"].Deserialize<SessionHostOfTheHub>(Json)!;

      HttpRequestMessage Request(AnnouncementOfTheHub hub) => new(HttpMethod.Post, hub.AddressOf("session-hosts")) { Content = JsonContent.Create(body) };
   }

   /// <summary>
   /// Ends the session of <paramref name="host"/>, as closing a terminal does: its shell and Claude Code get the console's
   /// close event. Returns once the host has ended, or was ending already, or isn't there.
   /// </summary>
   public async Task End(SessionHostId host, CancellationToken cancellation = default)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Post, it.AddressOf($"session-hosts/{host}/end")), cancellation);
      if(answer.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound) return;
      throw new HttpRequestException($"The hub answered ending the session host {host} with {(int)answer.StatusCode}: {await answer.Content.ReadAsStringAsync(cancellation)}");
   }

   /// <summary>
   /// Has the hub type <paramref name="command"/> into the shell of <paramref name="host"/>, then Enter, once the shell
   /// has settled at its prompt: a <see cref="RefusedByTheHub"/> when it doesn't, as when a program runs in the shell.
   /// </summary>
   public async Task StartClaudeCodeIn(SessionHostOfTheHub host, ClaudeCodeCommand command, CancellationToken cancellation = default)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Post, it.AddressOf($"shells/{host.ShellProcessId}/start-claude-code")) { Content = JsonContent.Create(command.Body) }, cancellation);
      if(answer.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
      {
         var refusal = await JsonOf(answer, cancellation);
         throw new RefusedByTheHub((string)refusal["error"]!);
      }

      await JsonOf(answer, cancellation);
   }

   /// <summary>
   /// Every running Claude Code, each time the list changes, the whole list each time: from the hub's
   /// <c>GET /running-sessions/stream</c>, and from the next hub's when a publish ends it, until
   /// <paramref name="cancellation"/>.
   /// </summary>
   public async IAsyncEnumerable<IReadOnlyList<RunningSession>> RunningSessionsAsTheyChange([EnumeratorCancellation] CancellationToken cancellation)
   {
      while(!cancellation.IsCancellationRequested)
      {
         using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Get, it.AddressOf("running-sessions/stream")), cancellation, HttpCompletionOption.ResponseHeadersRead);
         if(answer.StatusCode == HttpStatusCode.ServiceUnavailable) // Asked for while the hub stops, as at a publish.
         {
            await Task.Delay(BetweenTries, cancellation);
            continue;
         }

         answer.EnsureSuccessStatusCode();
         using var events = new StreamReader(await answer.Content.ReadAsStreamAsync(cancellation));
         await foreach(var sent in ServerSentEvent.ReadFrom(events, cancellation))
            if(sent.Name == "running-sessions")
               yield return JsonNode.Parse(sent.Data)!["runningSessions"].Deserialize<List<RunningSession>>(Json)!;
      }
   }

   /// <summary>
   /// Sends the request <paramref name="request"/> makes for the hub announced now, and again for the next hub announced
   /// while the hub announced doesn't answer, as at a publish, for <see cref="HowLongANewHubMayTakeToAnswer"/>.
   /// </summary>
   async Task<HttpResponseMessage> Send(Func<AnnouncementOfTheHub, HttpRequestMessage> request, CancellationToken cancellation, HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
   {
      var time = new TimeToAnswer(_folderOfTheAnnouncement);
      while(true)
      {
         if(await Answer(await TheAnnouncement(time, cancellation)) is {} answer) return answer;
         await time.WaitBeforeTryingAgain(cancellation);
      }

      async Task<HttpResponseMessage?> Answer(AnnouncementOfTheHub announcement)
      {
         if(announcement.Protocol != ProtocolCloydSpeaks)
            throw new InvalidOperationException($"The hub speaks protocol {announcement.Protocol}, and this Cloyd protocol {ProtocolCloydSpeaks}: Cloyd needs updating.");

         var asked = request(announcement);
         asked.Headers.Authorization = new AuthenticationHeaderValue("Bearer", announcement.Token);
         try
         {
            var answer = await _http.SendAsync(asked, completion, cancellation);
            if(answer.StatusCode != HttpStatusCode.Unauthorized) return answer;

            answer.Dispose(); // A new hub, with a token of its own, may have taken the port.
            return null;
         }
         catch(HttpRequestException failure) when(failure.HttpRequestError == HttpRequestError.ConnectionError)
         {
            return null; // The hub announced has stopped, as at a publish, or crashed and left its announcement.
         }
      }
   }

   /// <summary>
   /// The hub's announcement, once there is one, within <paramref name="time"/>: the published hub is started when there
   /// is none.
   /// </summary>
   async Task<AnnouncementOfTheHub> TheAnnouncement(TimeToAnswer time, CancellationToken cancellation)
   {
      var startedThePublishedHub = false;
      while(true)
      {
         if(AnnouncementOfTheHub.In(_folderOfTheAnnouncement) is {} announcement) return announcement;
         if(_isThePublishedHub && !startedThePublishedHub)
         {
            StartThePublishedHub();
            startedThePublishedHub = true;
         }

         await time.WaitBeforeTryingAgain(cancellation);
      }
   }

   /// <summary>
   /// Starts the published hub, <c>~/.claude/bin/ClaudeTooling.exe hub start</c>, which starts it detached, outside every
   /// job, unless one runs: every Claude Code session's start does the same.
   /// </summary>
   static void StartThePublishedHub()
   {
      var client = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin", "ClaudeTooling.exe");
      var start = new ProcessStartInfo(client) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden, ArgumentList = { "hub", "start" } };
      using var started = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {client}.");
      if(!started.WaitForExit(HowLongANewHubMayTakeToAnswer)) throw new TimeoutException($"{client} hub start didn't exit within {HowLongANewHubMayTakeToAnswer.TotalSeconds:0} s.");
   }

   /// <summary>The JSON of <paramref name="answer"/>: an <see cref="HttpRequestException"/> with its body for an answer that isn't a success.</summary>
   static async Task<JsonNode> JsonOf(HttpResponseMessage answer, CancellationToken cancellation)
   {
      var body = await answer.Content.ReadAsStringAsync(cancellation);
      if(answer.StatusCode is not (HttpStatusCode.Conflict or HttpStatusCode.NotFound) && !answer.IsSuccessStatusCode)
         throw new HttpRequestException($"{answer.RequestMessage?.Method} {answer.RequestMessage?.RequestUri?.AbsolutePath}: {(int)answer.StatusCode} {body}");
      return JsonNode.Parse(body)!;
   }
}
