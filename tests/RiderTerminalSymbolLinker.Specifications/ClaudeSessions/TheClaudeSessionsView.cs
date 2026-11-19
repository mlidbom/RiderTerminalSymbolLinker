using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;
using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

/// <summary>
///   The Claude Sessions tool window in the sandbox Rider, as the specifications see and drive it through Claude
///   Sessions' development observation channel. Its tabs run the sandbox's fake Claude Code, <c>tools/FakeClaudeCode</c>.
/// </summary>
/// <remarks>
///   It starts with no tabs, no session hosts, no sessions saved, and its terminals running
///   <see cref="TheShellStoriesStartIn"/>: every story starts from there. A tab's session runs in a session host of the
///   sandbox's hub, outside the sandbox Rider, and outlives it.
/// </remarks>
public sealed class TheClaudeSessionsView
{
   /// <summary>The shell the view's terminals run as a story starts: Magnus's, PowerShell 7.</summary>
   public const string TheShellStoriesStartIn = "pwsh.exe";

   /// <summary>How long the view may take to show a change: the keeper looks every two seconds, the fake starts in less.</summary>
   static readonly TimeSpan HowLongAChangeMayTake = TimeSpan.FromSeconds(15);

   readonly SandboxRider _sandbox = new(RiderInstallation.OfThisUser());

   ClaudeSessionsObservationChannel Channel => _sandbox.ClaudeSessionsObservation;

   public TheClaudeSessionsView()
   {
      Channel.OpenTheView();
      if(Picker.Shown) CloseThePicker();
      if(NewSessionPopup.Shown) CloseTheNewSessionPopup();
      RunTheShell(TheShellStoriesStartIn);
      Channel.CloseEveryTab();
      _sandbox.EndItsSessions(); // Those no tab shows, as when a story ended a tab's attach client.
      WaitUntil(() => Tabs.Count == 0 && SavedTabs.Count == 0 && SessionHosts.Count == 0, "the view to have no tabs and no session hosts, and no tabs saved");
      ForgetTheHistory();
   }

   /// <summary>Deletes the transcripts of every session the sandbox's Claude Code ran, so that a story starts with no history.</summary>
   void ForgetTheHistory()
   {
      var transcripts = Path.Combine(_sandbox.ClaudeCodeFolder, "projects");
      if(Directory.Exists(transcripts)) Directory.Delete(transcripts, recursive: true);
   }

   /// <summary>Writes <paramref name="transcript"/> where the sandbox's Claude Code keeps its sessions' transcripts.</summary>
   public SessionTranscript Write(SessionTranscript transcript)
   {
      transcript.WriteTo(_sandbox.ClaudeCodeFolder);
      return transcript;
   }

   /// <summary>Where the sandbox's Claude Sessions finds other IDEs' announcements of their local APIs.</summary>
   public string LocalApiAnnouncementsFolder => _sandbox.LocalApiAnnouncementsFolder;

   /// <summary>
   ///   Runs the sandbox's fake Claude Code outside the view, as in a plain terminal, resuming <paramref name="session"/>
   ///   in <paramref name="folder"/>, and returns once the sandbox's hub has told the view of it.
   /// </summary>
   public ClaudeCodeOutsideTheView RunClaudeCodeOutsideTheView(string session, string folder)
   {
      var start = new ProcessStartInfo(_sandbox.FakeClaudeCode, ["--resume", session])
      {
         WorkingDirectory = folder,
         UseShellExecute = false,
         RedirectStandardInput = true,
         RedirectStandardOutput = true,
         Environment = { ["CLAUDE_CONFIG_DIR"] = _sandbox.ClaudeCodeFolder }
      };
      var claudeCode = new ClaudeCodeOutsideTheView(Process.Start(start)!);
      WaitUntil(() => RunningClaudeCodes.Contains(claudeCode.Process.Id), "the sandbox's hub to tell the view of the Claude Code outside it");
      return claudeCode;
   }

   /// <summary>The processes of the Claude Codes that the sandbox's hub last told the view of.</summary>
   public IReadOnlyList<int> RunningClaudeCodes => [.. Channel.State()["runningClaudeCodes"]!.AsArray().Select(it => it!["processId"]!.GetValue<int>())];

   /// <summary>The session history picker shown in the project's window.</summary>
   public HistoryPicker Picker => HistoryPicker.From(Channel.ThePicker());

   /// <summary>Opens the session history picker, as the Resume a Claude Session action does, and returns it once it shows.</summary>
   public HistoryPicker OpenThePicker() => HistoryPicker.From(Channel.OpenThePicker());

   /// <summary>Types <paramref name="text"/> into the picker's search field.</summary>
   public HistoryPicker FilterThePicker(string text) => HistoryPicker.From(Channel.FilterThePicker(text));

   /// <summary>Has the picker list <paramref name="scope"/>, such as <c>ALL_PROJECTS</c>, as a click on its button does.</summary>
   public HistoryPicker ListInThePicker(string scope) => HistoryPicker.From(Channel.ListInThePicker(scope));

   /// <summary>Chooses the picker's entry <paramref name="entry"/>, by its place, as Enter on it does.</summary>
   public HistoryPicker ChooseInThePicker(int entry) => HistoryPicker.From(Channel.ChooseInThePicker(entry));

   /// <summary>Closes the picker, as Escape does.</summary>
   public HistoryPicker CloseThePicker() => HistoryPicker.From(Channel.CloseThePicker());

   /// <summary>Presses <paramref name="key"/>, <c>UP</c>, <c>DOWN</c> or <c>ENTER</c>, in the picker's search field.</summary>
   public HistoryPicker PressInThePicker(string key) => HistoryPicker.From(Channel.PressInThePicker(key));

   /// <summary>Double-clicks the picker's entry <paramref name="entry"/>, by its place.</summary>
   public HistoryPicker DoubleClickInThePicker(int entry) => HistoryPicker.From(Channel.DoubleClickInThePicker(entry));

   /// <summary>The New Claude Session popup shown in the project's window.</summary>
   public NewSessionPopup NewSessionPopup => NewSessionPopup.From(Channel.TheNewSessionPopup());

   /// <summary>Opens the New Claude Session popup, as the New Claude Session action does.</summary>
   public NewSessionPopup OpenTheNewSessionPopup() => NewSessionPopup.From(Channel.OpenTheNewSessionPopup());

   /// <summary>Types <paramref name="name"/> into the New Claude Session popup's field.</summary>
   public NewSessionPopup NameInTheNewSessionPopup(string name) => NewSessionPopup.From(Channel.NameInTheNewSessionPopup(name));

   /// <summary>Presses Enter in the New Claude Session popup's field, and returns the tab it started the session in, once the session runs.</summary>
   public SessionTab PressEnterInTheNewSessionPopup() => TabOnceItRunsASession(Channel.PressEnterInTheNewSessionPopup()["tab"]!.GetValue<int>());

   /// <summary>Closes the New Claude Session popup, as Escape does.</summary>
   public NewSessionPopup CloseTheNewSessionPopup() => NewSessionPopup.From(Channel.CloseTheNewSessionPopup());

   /// <summary>Has the view's terminals run the shell <paramref name="path"/> from now on, such as <c>cmd.exe</c>.</summary>
   public void RunTheShell(string path) => Channel.RunTheShell(path);

   /// <summary>The actions the sandbox's active keymap binds <paramref name="keystroke"/> to, such as <c>ctrl alt shift H</c>.</summary>
   public IReadOnlyList<string> ActionsBoundTo(string keystroke) => [.. Channel.ActionsBoundTo(keystroke)["actions"]!.AsArray().Select(it => it!.GetValue<string>())];

   /// <summary>The actions the keymap named <paramref name="keymap"/>, such as <c>Visual Studio 2022</c>, binds <paramref name="keystroke"/> to.</summary>
   public IReadOnlyList<string> ActionsBoundTo(string keystroke, string keymap) =>
      [.. Channel.ActionsBoundTo(keystroke, keymap)["actions"]!.AsArray().Select(it => it!.GetValue<string>())];

   /// <summary>The chat of tab <paramref name="tab"/>, which every tab has in the sandbox, with a stand-in behind it.</summary>
   public TheChat ChatOf(int tab) => new(Channel, tab);

   /// <summary>The folder of the project open in the sandbox, the fixture solution's.</summary>
   public string ProjectFolder => Path.GetFullPath(Project()["basePath"]!.GetValue<string>());

   /// <summary>The tab the view shows, by its place among the tabs, or null when it has none.</summary>
   public int? SelectedTab => Project()["view"]!["selectedTab"]?.GetValue<int>();

   /// <summary>Selects the tab, as a click on it does.</summary>
   public void Select(int tab)
   {
      Channel.Select(tab);
      WaitUntil(() => SelectedTab == tab, $"tab {tab} to be selected");
   }

   /// <summary>The process the sandbox Rider runs as.</summary>
   public int SandboxProcessId => _sandbox.ProcessId;

   /// <summary>
   ///   The file in which the sandbox Rider's Claude Sessions announces its local API to other tools on the machine:
   ///   <c>&lt;its process id&gt;.json</c> in <see cref="LocalApiAnnouncementsFolder"/>, or null while there is none.
   /// </summary>
   public JsonNode? LocalApiAnnouncement
   {
      get
      {
         var file = Path.Combine(_sandbox.LocalApiAnnouncementsFolder, $"{_sandbox.ProcessId}.json");
         return File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) : null;
      }
   }

   /// <summary>
   ///   Asks Claude Sessions' local API to show the tab that runs <paramref name="session"/>, or in which Claude Code runs
   ///   as <paramref name="claudeCodeProcess"/>, as a tool whose notification about the session the user clicked does,
   ///   and returns the answer's status code.
   /// </summary>
   public int AskTheLocalApiToShow(string? session, long? claudeCodeProcess)
   {
      var port = LocalApiAnnouncement!["port"]!.GetValue<int>();
      var query = string.Join("&", new[] { session is null ? null : $"session={session}", claudeCodeProcess is null ? null : $"process={claudeCodeProcess}" }.OfType<string>());
      using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
      return (int)client.PostAsync($"http://127.0.0.1:{port}/sessions/show?{query}", null).Result.StatusCode;
   }

   /// <summary>
   ///   Asks Claude Sessions' local API <paramref name="method"/> <paramref name="path"/>, with <paramref name="body"/> as
   ///   JSON if any, and the announcement's token when <paramref name="withTheToken"/>, naming <paramref name="host"/> as
   ///   its <c>Host</c>, or else the API's own address, and returns the answer: its status code and its body.
   /// </summary>
   public (int Status, JsonNode? Body) AskTheLocalApi(string method, string path, object? body = null, bool withTheToken = false, string? host = null)
   {
      var announcement = LocalApiAnnouncement!;
      var port = announcement["port"]!.GetValue<int>();
      using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
      using var request = new HttpRequestMessage(new HttpMethod(method), $"http://127.0.0.1:{port}{path}");
      if(body is not null) request.Content = JsonContent.Create(body);
      if(withTheToken) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", announcement["token"]!.GetValue<string>());
      if(host is not null) request.Headers.Host = host;
      using var answer = client.Send(request);
      var text = answer.Content.ReadAsStringAsync().Result;
      return ((int)answer.StatusCode, text.Length > 0 ? JsonNode.Parse(text) : null);
   }

   /// <summary>
   ///   Follows the sandbox Rider through a restart it makes itself, as <c>POST /restart</c> has it do, from the process
   ///   <paramref name="before"/> it ran as, and returns once the view has its tabs back, each with its session.
   /// </summary>
   public void FollowTheIdesRestart(int before, int tabsToComeBack)
   {
      _sandbox.FollowItsRestart(before, _sandbox.FixtureSolution);
      WaitUntil(() => Tabs.Count == tabsToComeBack && Tabs.All(it => it.SessionId is not null), $"{tabsToComeBack} tabs to be back with their sessions");
   }

   /// <summary>
   ///   Has the sandbox Rider restart itself through Claude Sessions' local API, as the tool for restarting Rider does, and
   ///   returns once it runs again with the fixture solution open: its other solutions may still be opening.
   /// </summary>
   public void RestartTheIdeThroughItsLocalApi()
   {
      var before = SandboxProcessId;
      var asked = AskTheLocalApi("POST", "/restart", new { why = "A specification restarts it", askedBy = "the specifications" }, withTheToken: true);
      if(asked.Status != 202) throw new InvalidOperationException($"The IDE refused to restart: {asked.Status} {asked.Body}");
      _sandbox.FollowItsRestart(before, _sandbox.FixtureSolution);
   }

   /// <summary>The folders of the projects whose windows the sandbox Rider shows, as Claude Sessions' <c>GET /windows</c> tells of them.</summary>
   public IReadOnlyList<string> ProjectWindows =>
      [.. AskTheLocalApi("GET", "/windows").Body!["windows"]!.AsArray().Select(it => Path.GetFullPath(it!["project"]!.GetValue<string>())).Order()];

   /// <summary>
   ///   Opens a fresh copy of the fixture solution, in a folder of its own named <paramref name="name"/>, in a window of
   ///   its own, and returns its folder. <see cref="CloseTheCopiesOfTheFixtureSolution"/> closes it, for the next story.
   /// </summary>
   public string OpenAnotherCopyOfTheFixtureSolution(string name)
   {
      var solution = _sandbox.AnotherCopyOfTheFixtureSolution(name);
      _sandbox.OpenAlso(solution);
      return Path.GetDirectoryName(solution)!;
   }

   /// <summary>
   ///   Ends the launchers that handed the copies of the fixture solution to the sandbox Rider, once their windows show:
   ///   one that stayed was there as Rider restarted, and held the solutions folder.
   /// </summary>
   public void EndTheLaunchersOfTheCopies() => _sandbox.EndTheLaunchersOfOtherSolutions();

   /// <summary>
   ///   Closes the copies of the fixture solution <see cref="OpenAnotherCopyOfTheFixtureSolution"/> opened as
   ///   <paramref name="names"/>: kills the sandbox Rider, deletes them, so that it doesn't open them again, and starts it
   ///   again with the fixture solution alone. Killed, not stopped: closed one window after the other, Rider showed its
   ///   Rider Home window in place of the last, and didn't exit (2026-11-19).
   /// </summary>
   public void CloseTheCopiesOfTheFixtureSolution(params string[] names)
   {
      _sandbox.Kill();
      foreach(var name in names) _sandbox.DeleteTheCopyOfTheFixtureSolution(name);
      _sandbox.Start(_sandbox.FixtureSolution);
   }

   /// <summary>Whether the IDE shows the view's tool window: starting a session shows it.</summary>
   public bool IsShown => Project()["view"]!["toolWindowVisible"]!.GetValue<bool>();

   /// <summary>The view's tabs, in their order.</summary>
   public IReadOnlyList<SessionTab> Tabs =>
   [
      .. Project()["view"]!["tabs"]!.AsArray().Select(tab => new SessionTab(tab!["shellProcessId"]?.GetValue<long>(),
                                                                            tab["claudeCode"] is {} claudeCode
                                                                               ? new ClaudeCodeProcess(claudeCode["processId"]!.GetValue<long>(),
                                                                                                       claudeCode["parentProcessId"]?.GetValue<long>())
                                                                               : null,
                                                                            tab["session"]?["id"]!.GetValue<string>(),
                                                                            tab["session"]?["workingDirectory"]!.GetValue<string>(),
                                                                            tab["session"]?["name"]?.GetValue<string>(),
                                                                            tab["sessionHost"]?.GetValue<string>()))
   ];

   /// <summary>The process each tab's terminal runs, in their order: its session host's attach client, once the tab has been shown.</summary>
   public IReadOnlyList<long?> AttachClients => [.. Project()["view"]!["tabs"]!.AsArray().Select(tab => tab!["processId"]?.GetValue<long>())];

   /// <summary>The names the view shows its tabs by, in their order.</summary>
   public IReadOnlyList<string> TabNames => [.. Project()["view"]!["tabs"]!.AsArray().Select(tab => tab!["name"]!.GetValue<string>())];

   /// <summary>The tabs saved for the next start, in their order, each with its session, once it has one, and its session host.</summary>
   public IReadOnlyList<SavedTab> SavedTabs =>
      [.. Project()["saved"]!["tabs"]!.AsArray().Select(it => new SavedTab(it!["session"]?["id"]!.GetValue<string>(), it["sessionHost"]?.GetValue<string>()))];

   /// <summary>The ids of the sessions saved for the next start, in the order of the tabs that ran them.</summary>
   public IReadOnlyList<string> SavedSessions => [.. SavedTabs.Select(it => it.SessionId).OfType<string>()];

   /// <summary>
   ///   The ids of the session hosts that run, as the sandbox's hub lists them, outside the sandbox Rider: each the
   ///   console of a tab's session, which outlives the sandbox Rider.
   /// </summary>
   public IReadOnlyList<string> SessionHosts => [.. _sandbox.SessionHosts().Select(it => it!["id"]!.GetValue<string>())];

   /// <summary>Whether the session host <paramref name="id"/> has an attach client connected, as its hub lists it.</summary>
   public bool IsAttached(string id) => _sandbox.SessionHosts().Single(it => it!["id"]!.GetValue<string>() == id)!["attached"]!.GetValue<bool>();

   /// <summary>
   ///   Starts a new session, without a name, as the New Claude Session popup does with its field left empty, and returns
   ///   its tab once the session runs.
   /// </summary>
   public SessionTab StartANewSession() => TabOnceItRunsASession(Channel.StartANewSession()["tab"]!.GetValue<int>());

   SessionTab TabOnceItRunsASession(int tab)
   {
      WaitUntil(() => Tabs.Count > tab && Tabs[tab].SessionId is not null, "the new tab to run a session");
      return Tabs[tab];
   }

   /// <summary>
   ///   Opens a plain terminal tab in the view's tool window, not through the view: a tab can come into it that the view
   ///   didn't open. Returns its place among the view's tabs.
   /// </summary>
   public int OpenAPlainTerminalTab() => Channel.OpenAPlainTerminalTab()["tab"]!.GetValue<int>();

   /// <summary>Types <paramref name="text"/> into the tab's terminal, and Enter.</summary>
   public void Type(int tab, string text) => Channel.Type(tab, text);

   /// <summary>
   ///   Types <paramref name="line"/> into the tab's Claude Code, as a line of its conversation, and returns once the fake
   ///   Claude Code shows it: it has then written it to the session's transcript.
   /// </summary>
   public void Say(int tab, string line)
   {
      Type(tab, line);
      WaitUntil(() => Screen(tab).Contains($"> {line}"), $"the tab to show \"{line}\"");
   }

   /// <summary>
   ///   Deletes the record in which the tab's Claude Code names its session, <c>sessions/&lt;its process id&gt;.json</c>,
   ///   and returns once the view no longer finds the Claude Code by it: as if the view couldn't read the record.
   /// </summary>
   public void DeleteTheSessionRecordOfTheClaudeCodeIn(int tab)
   {
      File.Delete(Path.Combine(_sandbox.ClaudeCodeFolder, "sessions", $"{Tabs[tab].ClaudeCode!.ProcessId}.json"));
      WaitUntil(() => Tabs[tab].ClaudeCode is null, "the view to no longer find the tab's Claude Code");
   }

   /// <summary>Types <c>/exit</c> into the tab's Claude Code, and returns once it has exited.</summary>
   public void ExitClaudeCode(int tab)
   {
      Type(tab, "/exit");
      WaitUntil(() => Tabs[tab].ClaudeCode is null, "Claude Code to exit in the tab");
   }

   /// <summary>Deletes the transcript of the session, as archiving or deleting the session does, so that it can't be resumed.</summary>
   public void DeleteTheTranscriptOf(string session)
   {
      foreach(var transcript in Directory.GetFiles(Path.Combine(_sandbox.ClaudeCodeFolder, "projects"), session + ".jsonl", SearchOption.AllDirectories))
         File.Delete(transcript);
   }

   /// <summary>The text the tab's terminal shows.</summary>
   public IReadOnlyList<string> Screen(int tab) => [.. Channel.Screen(tab)["lines"]!.AsArray().Select(it => it!.GetValue<string>())];

   /// <summary>The size of the tab's terminal screen, or null while the tab hasn't been laid out.</summary>
   public TerminalSize? SizeOf(int tab) => Channel.Screen(tab)["size"] is {} size ? new TerminalSize(size["columns"]!.GetValue<int>(), size["rows"]!.GetValue<int>()) : null;

   /// <summary>
   ///   The size the tab's fake Claude Code last said its terminal has, the size Claude Code would draw itself at, once it
   ///   is the tab's size, or else whatever it is after <see cref="HowLongAChangeMayTake"/>: null if it said none.
   /// </summary>
   public TerminalSize? SizeTheClaudeCodeDrawsAtIn(int tab)
   {
      var stopwatch = Stopwatch.StartNew();
      while(LastSaid() != SizeOf(tab) && stopwatch.Elapsed < HowLongAChangeMayTake) Thread.Sleep(250);
      return LastSaid();

      TerminalSize? LastSaid() => TerminalSize.TheFakeClaudeCodeLastSaidIn(Screen(tab));
   }

   /// <summary>Waits until <paramref name="condition"/> holds, as the view takes in what happened.</summary>
   public void WaitUntil(Func<bool> condition, string whatItWaitsFor) => WaitUntil(condition, whatItWaitsFor, HowLongAChangeMayTake);

   /// <summary>Waits until <paramref name="condition"/> holds, for as long as <paramref name="itMayTake"/>.</summary>
   public static void WaitUntil(Func<bool> condition, string whatItWaitsFor, TimeSpan itMayTake)
   {
      var stopwatch = Stopwatch.StartNew();
      while(!condition())
      {
         if(stopwatch.Elapsed > itMayTake) throw new TimeoutException($"Gave up after {itMayTake} waiting for {whatItWaitsFor}.");
         Thread.Sleep(250);
      }
   }

   /// <summary>
   ///   Closes the sandbox Rider as a user would, and starts it again, and returns once the view has its tabs back, each
   ///   with its session: the sessions run on in their session hosts meanwhile.
   /// </summary>
   public void RestartTheIde(int tabsToComeBack)
   {
      _sandbox.Stop();
      _sandbox.Start(_sandbox.FixtureSolution);
      WaitUntil(() => Tabs.Count == tabsToComeBack && Tabs.All(it => it.SessionId is not null), $"{tabsToComeBack} tabs to be back with their sessions");
   }

   /// <summary>
   ///   Kills the sandbox Rider, as a crash ends it, and starts it again, and returns once the view has its tabs back, each
   ///   with its session: the sessions run on in their session hosts meanwhile.
   /// </summary>
   public void CrashTheIdeAndStartItAgain(int tabsToComeBack)
   {
      _sandbox.Kill();
      _sandbox.Start(_sandbox.FixtureSolution);
      WaitUntil(() => Tabs.Count == tabsToComeBack && Tabs.All(it => it.SessionId is not null), $"{tabsToComeBack} tabs to be back with their sessions");
   }

   /// <summary>
   ///   Closes the sandbox Rider as a user would, ends the session hosts its sessions ran in, as a reboot does, and starts
   ///   it again, and returns once the view has its tabs, each with its Claude Code running.
   /// </summary>
   public void RestartTheIdeAsAfterAReboot(int tabsToComeBack)
   {
      _sandbox.StopAndEndItsSessions();
      _sandbox.Start(_sandbox.FixtureSolution);
      WaitUntil(() => Tabs.Count == tabsToComeBack && Tabs.All(it => it.ClaudeCode is not null), $"{tabsToComeBack} tabs to run their sessions again");
   }

   /// <summary>Closes the tab, as its close button does: its session ends.</summary>
   public void Close(int tab) => Channel.Close(tab);

   /// <summary>
   ///   Ends the process of the tab's terminal, its session host's attach client, as when another terminal attaches to
   ///   the session and takes it over, or the attach client crashes: the tab closes, and its session runs on.
   /// </summary>
   public void EndTheAttachClientOf(int tab) => Process.GetProcessById((int)AttachClients[tab]!.Value).Kill();

   /// <summary>Has the sandbox reload Claude Sessions, as for an update, and returns once the reloaded plugin answers.</summary>
   public void ReloadThePlugin() => _sandbox.ReloadThePlugin(OurPlugin.ClaudeSessions);

   JsonNode Project() => Channel.State()["projects"]!.AsArray().Single()!;
}

/// <summary>
///   A tab of the view: its shell, which runs in its session host; the Claude Code that runs below it, while one does;
///   the session that runs in it, or ran in it last, with the folder it runs in and its name; and its session host.
/// </summary>
public sealed record SessionTab(long? ShellProcessId, ClaudeCodeProcess? ClaudeCode, string? SessionId, string? WorkingDirectory, string? SessionName, string? SessionHost);

/// <summary>A tab as the view saves it for the next start: its session, once it has one, and its session host.</summary>
public sealed record SavedTab(string? SessionId, string? SessionHost);

/// <summary>The New Claude Session popup, as the observation channel tells of it: whether it's shown, and the name typed into it.</summary>
public sealed record NewSessionPopup(bool Shown, string? Name)
{
   public static NewSessionPopup From(JsonNode popup) => new(popup["shown"]!.GetValue<bool>(), popup["name"]?.GetValue<string>());
}

/// <summary>The size of a terminal's screen, in the characters across it and the lines down it.</summary>
public sealed record TerminalSize(int Columns, int Rows)
{
   /// <summary>The line in which the fake Claude Code says the size of its terminal, as it starts and each time it changes.</summary>
   static readonly Regex TheLineInWhichTheFakeClaudeCodeSaysItsSize = new(@"^The terminal is (?<columns>\d+) columns by (?<rows>\d+) rows$");

   /// <summary>The size of its terminal that the fake Claude Code said last in <paramref name="lines"/>, or null if none says one.</summary>
   public static TerminalSize? TheFakeClaudeCodeLastSaidIn(IEnumerable<string> lines) =>
      lines.Select(line => TheLineInWhichTheFakeClaudeCodeSaysItsSize.Match(line)).LastOrDefault(it => it.Success) is {} said
         ? new TerminalSize(int.Parse(said.Groups["columns"].Value), int.Parse(said.Groups["rows"].Value))
         : null;
}

/// <summary>The process of a Claude Code that runs in a tab, and the process it's a child of.</summary>
public sealed record ClaudeCodeProcess(long ProcessId, long? ParentProcessId);

/// <summary>A fake Claude Code that runs outside the view, as in a plain terminal: disposing it ends it.</summary>
public sealed class ClaudeCodeOutsideTheView(Process process) : IDisposable
{
   public Process Process { get; } = process;

   public void Dispose()
   {
      Process.Kill();
      Process.WaitForExit();
      Process.Dispose();
   }
}

/// <summary>
///   The session history picker, as the observation channel tells of it: whether it's shown, the scope it lists, the
///   scopes it offers, its filter, the entry selected, and its entries.
/// </summary>
public sealed record HistoryPicker(bool Shown, string? Scope, IReadOnlyList<string> Scopes, string? Filter, int? Selected, IReadOnlyList<PickerEntry> Entries)
{
   public static HistoryPicker From(JsonNode picker) =>
      picker["shown"]!.GetValue<bool>()
         ? new HistoryPicker(true,
                             picker["scope"]!.GetValue<string>(),
                             [.. picker["scopes"]!.AsArray().Select(it => it!.GetValue<string>())],
                             picker["filter"]!.GetValue<string>(),
                             picker["selected"]!.GetValue<int>(),
                             [.. picker["entries"]!.AsArray().Select(it => PickerEntry.From(it!))])
         : new HistoryPicker(false, null, [], null, null, []);

   /// <summary>The ids of the sessions of its entries, in their order.</summary>
   public IReadOnlyList<string> Sessions => [.. Entries.Select(it => it.Session)];
}

/// <summary>
///   An entry of the session history picker: its session, what the picker calls it, the last prompt it shows after a
///   name, the folder the session last ran in, its branch, where the session is now, and whether it can be chosen.
/// </summary>
/// <param name="Where">
///   <c>not-running</c>, <c>exited-in-a-tab</c>, <c>running-in-a-tab</c>, <c>running-in-another-ide</c> or
///   <c>running-outside-claude-sessions</c>.
/// </param>
public sealed record PickerEntry(string Session, string Title, string? LastPrompt, string Folder, string? GitBranch, string Where, bool CanBeChosen)
{
   public static PickerEntry From(JsonNode entry) =>
      new(entry["session"]!.GetValue<string>(),
          entry["title"]!.GetValue<string>(),
          entry["lastPrompt"]?.GetValue<string>(),
          entry["folder"]!.GetValue<string>(),
          entry["gitBranch"]?.GetValue<string>(),
          entry["where"]!.GetValue<string>(),
          entry["canBeChosen"]!.GetValue<bool>());
}
