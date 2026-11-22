using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;
using ClaudeTooling.Hub.LocalApi.Contract.HostingSessions;
using ClaudeTooling.Hub.LocalApi.Contract.ListingSessions;
using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;
using ClaudeTooling.Hub.LocalApi.Contract.TypingIntoRunningSessions;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat.Writing;
using Cloyd.ClaudeCode;
using Cloyd.Hub.Commands;
using Cloyd.Hub.RunningSessions;
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
   static readonly TimeSpan HowLongANewHubMayTakeToAnswer = TimeSpan.FromSeconds(30);
   static readonly TimeSpan BetweenTries = TimeSpan.FromMilliseconds(250);

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
      Environment.GetEnvironmentVariable(WhatTheAnnouncementSays.VariableNamingTheFolder) is {Length: > 0} folder
         ? AnnouncedIn(new DirectoryInfo(folder))
         : new TheHub(new DirectoryInfo(WhatTheAnnouncementSays.FolderOfThePublishedHub), isThePublishedHub: true);

   /// <summary>The hub that announces itself in <paramref name="folder"/>: one tried out apart from the published one, which whoever tries it starts.</summary>
   public static TheHub AnnouncedIn(DirectoryInfo folder) => new(folder, isThePublishedHub: false);

   /// <summary>What the hub currently offers a newly opened tab, before the window sees another running-session update.</summary>
   public bool SupportsCodexChat => AnnouncementOfTheHub.In(_folderOfTheAnnouncement)?.Says.SupportsCodexChat == true;

   /// <summary>The Claude Code folder the hub serves: where the sessions of its hosts keep their transcripts.</summary>
   public async Task<ClaudeCodeFolder> ClaudeCodeFolderItServes(CancellationToken cancellation = default) =>
      (await TheAnnouncement(new TimeToAnswer(_folderOfTheAnnouncement), cancellation)).ClaudeCodeFolderItServes;

   /// <summary>The agent CLIs whose sessions the hub starts and lists, as its announcement says: Claude Code first.</summary>
   public async Task<IReadOnlyList<AgentCli>> AgentClisItServes(CancellationToken cancellation = default) =>
      (await TheAnnouncement(new TimeToAnswer(_folderOfTheAnnouncement), cancellation)).AgentClisItServes;

   /// <summary>Every session host that runs, in the order they started.</summary>
   public async Task<IReadOnlyList<SessionHostOfTheHub>> SessionHosts(CancellationToken cancellation = default)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Get, it.AddressOf("session-hosts")), cancellation);
      return [.. (await Read<SessionHostsAnswer>(answer, cancellation)).SessionHosts.Select(SessionHostOfTheHub.InThe)];
   }

   /// <summary>
   /// Starts a session host running <paramref name="shell"/> in <paramref name="directory"/>, known by
   /// <paramref name="label"/>, and returns it once it listens. A host that didn't tell of itself in time, as on a very
   /// busy machine, the hub ends and answers 500 for: it is asked once more.
   /// </summary>
   public async Task<SessionHostOfTheHub> StartASessionHost(ShellOfNewSessions shell, string directory, string label, CancellationToken cancellation = default)
   {
      var body = new SessionHostToStartInTheRequest(shell.CommandLine, directory, Columns: null, Rows: null, new Dictionary<string, string>(shell.Environment), label);
      var answer = await Send(Request, cancellation);
      if(answer.StatusCode == HttpStatusCode.InternalServerError)
      {
         answer.Dispose();
         answer = await Send(Request, cancellation);
      }

      using(answer) return SessionHostOfTheHub.InThe((await Read<SessionHostStartedAnswer>(answer, cancellation)).SessionHost);

      HttpRequestMessage Request(AnnouncementOfTheHub hub) => new(HttpMethod.Post, hub.AddressOf("session-hosts")) { Content = JsonContent.Create(body, options: LocalApiProtocol.Json) };
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
   public Task StartClaudeCodeIn(SessionHostOfTheHub host, ClaudeCodeCommand command, CancellationToken cancellation = default) =>
      StartAnAgentCliIn(host, "start-claude-code", command.Request, cancellation);

   /// <summary>
   /// Has the hub type <paramref name="command"/> into the shell of <paramref name="host"/>, then Enter, once the shell
   /// has settled at its prompt: a <see cref="RefusedByTheHub"/> when it doesn't, as when a program runs in the shell.
   /// Only a hub that serves Codex sessions has it: one whose <see cref="AnnouncementOfTheHub"/> names a Codex folder.
   /// </summary>
   public Task StartCodexIn(SessionHostOfTheHub host, CodexCommand command, CancellationToken cancellation = default) =>
      StartAnAgentCliIn(host, "start-codex", command.Request, cancellation);

   /// <summary>Posts <paramref name="request"/> to the hub's <c>shells/{processId}/&lt;<paramref name="start"/>&gt;</c> for the shell of <paramref name="host"/>.</summary>
   async Task StartAnAgentCliIn<TRequest>(SessionHostOfTheHub host, string start, TRequest request, CancellationToken cancellation)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Post, it.AddressOf($"shells/{host.ShellProcessId}/{start}")) { Content = JsonContent.Create(request, options: LocalApiProtocol.Json) }, cancellation);
      if(answer.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
         throw new RefusedByTheHub((await Read<RefusalAnswer>(answer, cancellation)).Error);

      await Read<AgentCliStartedInTheShellAnswer>(answer, cancellation);
   }

   /// <summary>
   /// Has the hub type <paramref name="message"/> into the input box of <paramref name="claudeCode"/>, as Magnus would,
   /// and send it: answered once Claude Code has taken it, with whether it queued it, as it does while it works, to give it
   /// to the model once it can. A <see cref="RefusedByTheHub"/> with why when it didn't, as when Claude Code shows a dialog,
   /// or its own box holds text.
   /// </summary>
   public async Task<bool> Send(MessageFromTheChat message, ClaudeCodeProcessId claudeCode, CancellationToken cancellation = default)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Post, it.AddressOf($"running-sessions/{claudeCode}/send")) { Content = JsonContent.Create(message.InTheRequest(), options: LocalApiProtocol.Json) }, cancellation);
      if(answer.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
         throw new RefusedByTheHub((await Read<RefusalAnswer>(answer, cancellation)).Error);

      return (await Read<MessageSentAnswer>(answer, cancellation)).Queued;
   }

   /// <summary>
   /// Has the hub press Escape in <paramref name="claudeCode"/>, which interrupts its turn, as Magnus asks with the Stop
   /// button: a <see cref="RefusedByTheHub"/> with why when it didn't, as when Claude Code was done already.
   /// </summary>
   public async Task Interrupt(ClaudeCodeProcessId claudeCode, CancellationToken cancellation = default)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Post, it.AddressOf($"running-sessions/{claudeCode}/interrupt")), cancellation);
      if(answer.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
         throw new RefusedByTheHub((await Read<RefusalAnswer>(answer, cancellation)).Error);

      await Read<TypedIntoTheSessionAnswer>(answer, cancellation);
   }

   /// <summary>
   /// Every session on disk of the agent CLIs the hub serves, but the archived ones, the most recently used first: each
   /// Claude Code session with a transcript in the Claude Code folder the hub serves, and each Codex session Codex lists.
   /// </summary>
   public async Task<IReadOnlyList<SessionOnDisk>> SessionsOnDisk(CancellationToken cancellation = default)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Get, it.AddressOf("sessions")), cancellation);
      return SessionOnDisk.EachIn(await Read<SessionsAnswer>(answer, cancellation));
   }

   /// <summary>Every running session of an agent CLI Cloyd knows, as the hub lists them now.</summary>
   public async Task<IReadOnlyList<RunningSession>> RunningSessions(CancellationToken cancellation = default)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Get, it.AddressOf("running-sessions")), cancellation);
      return RunningSession.EachIn(await Read<RunningSessionsAnswer>(answer, cancellation));
   }

   /// <summary>
   /// Every running session of an agent CLI Cloyd knows, each time the list changes, the whole list each time: from the hub's
   /// <c>GET /running-sessions/stream</c>, and from the next hub's when a publish ends it, until
   /// <paramref name="cancellation"/>.
   /// </summary>
   public async IAsyncEnumerable<(AnnouncementOfTheHub Hub, IReadOnlyList<RunningSession> Sessions)> RunningSessionsAsTheyChange([EnumeratorCancellation] CancellationToken cancellation)
   {
      await foreach(var stream in EachStreamAt("running-sessions/stream", cancellation))
         await foreach(var sent in stream.Events)
            if(sent.Name == RunningSessionsStreamEventNames.RunningSessions)
               yield return (stream.Hub, RunningSession.EachIn(JsonSerializer.Deserialize<RunningSessionsAnswer>(sent.Data, LocalApiProtocol.Json)!));
   }

   /// <summary>
   /// The conversation of the line of <paramref name="session"/>: the sessions a <c>/clear</c> ended before it, its own, and
   /// the session a clear of it began, as far as Claude Code has written it. Each time the hub's
   /// <c>GET /sessions/{sessionId}/conversation/stream</c> has sent it as written: all of it first, then after each change,
   /// and all of it again from the next hub when a publish ends the stream, until <paramref name="cancellation"/>.
   /// </summary>
   /// <exception cref="RefusedByTheHub">The session has no transcript, and no Claude Code runs it.</exception>
   public async IAsyncEnumerable<ConversationAnswer> ConversationAsWritten(ClaudeCodeSessionId session, [EnumeratorCancellation] CancellationToken cancellation)
   {
      await foreach(var stream in EachStreamAt($"sessions/{session}/conversation/stream", cancellation))
      {
         var conversation = new ConversationAsStreamed();
         await foreach(var sent in stream.Events)
            switch(sent.Name)
            {
               case ConversationStreamEventNames.Message:
                  conversation.Add(JsonSerializer.Deserialize<MessageInTheAnswer>(sent.Data, LocalApiProtocol.Json)!);
                  break;
               case ConversationStreamEventNames.ToolResult:
                  conversation.Add(JsonSerializer.Deserialize<ToolResultInTheAnswer>(sent.Data, LocalApiProtocol.Json)!);
                  break;
               case ConversationStreamEventNames.Rewind:
                  conversation.Take(JsonSerializer.Deserialize<RewindInTheStream>(sent.Data, LocalApiProtocol.Json)!);
                  break;
               case ConversationStreamEventNames.CaughtUp:
                  yield return conversation.AsSentSoFar;
                  break;
            }
      }
   }

   /// <summary>
   /// The block the model writes, or wrote last, in the turn of <paramref name="claudeCode"/>, as the hub's
   /// <c>GET /running-sessions/{processId}/turn/stream</c> tells of it, and each time it changes: null while none is. From
   /// the next hub when a publish ends the stream, which tells of the block so far again, until
   /// <paramref name="cancellation"/>.
   /// </summary>
   /// <exception cref="RefusedByTheHub">No Claude Code runs as <paramref name="claudeCode"/>: it has exited.</exception>
   public async IAsyncEnumerable<BlockBeingWritten?> BlockBeingWrittenIn(ClaudeCodeProcessId claudeCode, [EnumeratorCancellation] CancellationToken cancellation)
   {
      await foreach(var stream in EachStreamAt($"running-sessions/{claudeCode}/turn/stream", cancellation))
      {
         var turn = new TurnAsStreamed();
         await foreach(var sent in stream.Events)
            if(turn.Read(sent))
               yield return turn.Block;
      }
   }

   /// <summary>
   /// The events of the hub's stream at <paramref name="path"/>, such as <c>running-sessions/stream</c>, a stream for each
   /// hub in turn: from the hub announced, and from the next hub when a publish ends it, which sends all of it again, until
   /// <paramref name="cancellation"/>. Each stream is read to its end before the next is asked for.
   /// </summary>
   /// <exception cref="RefusedByTheHub">The hub doesn't know what the stream would be of: it answered 404.</exception>
   async IAsyncEnumerable<(AnnouncementOfTheHub Hub, IAsyncEnumerable<ServerSentEvent> Events)> EachStreamAt(string path, [EnumeratorCancellation] CancellationToken cancellation)
   {
      while(!cancellation.IsCancellationRequested)
      {
         var (source, response) = await SendToTheAnnouncedHub(it => new HttpRequestMessage(HttpMethod.Get, it.AddressOf(path)), cancellation, HttpCompletionOption.ResponseHeadersRead);
         using var answer = response;
         if(answer.StatusCode == HttpStatusCode.ServiceUnavailable) // Asked for while the hub stops, as at a publish.
         {
            await Task.Delay(BetweenTries, cancellation);
            continue;
         }
         if(answer.StatusCode == HttpStatusCode.NotFound) throw new RefusedByTheHub((await Read<RefusalAnswer>(answer, cancellation)).Error);

         answer.EnsureSuccessStatusCode();
         using var events = new StreamReader(await answer.Content.ReadAsStreamAsync(cancellation));
         yield return (source, ServerSentEvent.ReadFrom(events, cancellation));

      }
   }

   /// <summary>
   /// Sends the request <paramref name="request"/> makes for the hub announced now, and again for the next hub announced
   /// while the hub announced doesn't answer, as at a publish, for <see cref="HowLongANewHubMayTakeToAnswer"/>.
   /// </summary>
   async Task<HttpResponseMessage> Send(Func<AnnouncementOfTheHub, HttpRequestMessage> request, CancellationToken cancellation) =>
      (await SendToTheAnnouncedHub(request, cancellation, HttpCompletionOption.ResponseContentRead)).Response;

   /// <summary>Returns the announcement of the hub that actually answered, including after a publish during a retry.</summary>
   async Task<(AnnouncementOfTheHub Hub, HttpResponseMessage Response)> SendToTheAnnouncedHub(Func<AnnouncementOfTheHub, HttpRequestMessage> request,
                                                                                              CancellationToken cancellation, HttpCompletionOption completion)
   {
      var time = new TimeToAnswer(_folderOfTheAnnouncement);
      while(true)
      {
         var announced = await TheAnnouncement(time, cancellation);
         if(await Answer(announced) is {} answer) return (announced, answer);
         await time.WaitBeforeTryingAgain(cancellation);
      }

      async Task<HttpResponseMessage?> Answer(AnnouncementOfTheHub announcement)
      {
         try
         {
            var answer = await SendOnce(announcement, request, completion, cancellation);
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

   /// <summary>Sends one authenticated request to this particular hub. A command with an uncertain outcome must
   /// never be submitted again merely because the hub stopped answering.</summary>
   async Task<HttpResponseMessage> SendOnce(AnnouncementOfTheHub announcement, Func<AnnouncementOfTheHub, HttpRequestMessage> request,
                                            HttpCompletionOption completion, CancellationToken cancellation)
   {
      if(announcement.Protocol != LocalApiProtocol.Version)
         throw new InvalidOperationException($"The hub speaks protocol {announcement.Protocol}, and this Cloyd protocol {LocalApiProtocol.Version}: Cloyd needs updating.");
      using var asked = request(announcement);
      asked.Headers.Authorization = new AuthenticationHeaderValue("Bearer", announcement.Token);
      return await _http.SendAsync(asked, completion, cancellation);
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

   /// <summary>
   /// What <paramref name="answer"/> says, as the contract's <typeparamref name="TAnswer"/>: an
   /// <see cref="HttpRequestException"/> with its body for an answer that is neither a success nor a refusal (409, 404).
   /// </summary>
   static async Task<TAnswer> Read<TAnswer>(HttpResponseMessage answer, CancellationToken cancellation)
   {
      var body = await answer.Content.ReadAsStringAsync(cancellation);
      if(answer.StatusCode is not (HttpStatusCode.Conflict or HttpStatusCode.NotFound) && !answer.IsSuccessStatusCode)
         throw new HttpRequestException($"{answer.RequestMessage?.Method} {answer.RequestMessage?.RequestUri?.AbsolutePath}: {(int)answer.StatusCode} {body}");
      return JsonSerializer.Deserialize<TAnswer>(body, LocalApiProtocol.Json)!;
   }
}
