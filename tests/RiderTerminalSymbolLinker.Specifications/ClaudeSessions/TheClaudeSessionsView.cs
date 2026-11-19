using System.Diagnostics;
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
///   It starts with no tabs, no sessions saved, and its terminals running <see cref="TheShellStoriesStartIn"/>: every
///   story starts from there.
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
      WaitUntil(() => Tabs.Count == 0 && SavedSessions.Count == 0, "the view to have no tabs, and no sessions saved");
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
   ///   in <paramref name="folder"/>, and returns once it has recorded the session.
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
      WaitUntil(() => File.Exists(Path.Combine(_sandbox.ClaudeCodeFolder, "sessions", $"{claudeCode.Process.Id}.json")), "the Claude Code outside the view to record its session");
      return claudeCode;
   }

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

   /// <summary>Whether the IDE shows the Terminal tool window, where the view's tabs wait while the plugin reloads.</summary>
   public bool TheTerminalToolWindowIsShown => Project()["terminalToolWindow"]!["visible"]?.GetValue<bool>() ?? false;

   /// <summary>Whether the IDE shows the view's tool window: starting a session shows it.</summary>
   public bool IsShown => Project()["view"]!["toolWindowVisible"]!.GetValue<bool>();

   /// <summary>The view's tabs, in their order.</summary>
   public IReadOnlyList<SessionTab> Tabs =>
   [
      .. Project()["view"]!["tabs"]!.AsArray().Select(tab => new SessionTab(tab!["processId"]?.GetValue<long>(),
                                                                            tab["claudeCode"] is {} claudeCode
                                                                               ? new ClaudeCodeProcess(claudeCode["processId"]!.GetValue<long>(),
                                                                                                       claudeCode["parentProcessId"]?.GetValue<long>())
                                                                               : null,
                                                                            tab["session"]?["id"]!.GetValue<string>(),
                                                                            tab["session"]?["workingDirectory"]!.GetValue<string>(),
                                                                            tab["session"]?["name"]?.GetValue<string>()))
   ];

   /// <summary>The names the view shows its tabs by, in their order.</summary>
   public IReadOnlyList<string> TabNames => [.. Project()["view"]!["tabs"]!.AsArray().Select(tab => tab!["name"]!.GetValue<string>())];

   /// <summary>The ids of the sessions saved for the next start, in the order of the tabs that ran them.</summary>
   public IReadOnlyList<string> SavedSessions => [.. Project()["saved"]!["sessions"]!.AsArray().Select(it => it!["id"]!.GetValue<string>())];

   /// <summary>
   ///   The processes the Terminal tool window's tabs run, where the view's tabs are parked while the plugin reloads, or
   ///   null for a tab whose process hasn't started.
   /// </summary>
   public IReadOnlyList<long?> TerminalToolWindowProcesses => [.. Project()["terminalToolWindow"]!["tabs"]!.AsArray().Select(it => it!["processId"]?.GetValue<long>())];

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
   public void WaitUntil(Func<bool> condition, string whatItWaitsFor)
   {
      var stopwatch = Stopwatch.StartNew();
      while(!condition())
      {
         if(stopwatch.Elapsed > HowLongAChangeMayTake) throw new TimeoutException($"Gave up after {HowLongAChangeMayTake} waiting for {whatItWaitsFor}.");
         Thread.Sleep(250);
      }
   }

   /// <summary>
   ///   Closes the sandbox Rider as a user would, and starts it again, and returns once the view has its tabs, each with
   ///   its Claude Code running.
   /// </summary>
   public void RestartTheIde(int tabsToComeBack)
   {
      _sandbox.Stop();
      _sandbox.Start();
      WaitUntil(() => Tabs.Count == tabsToComeBack && Tabs.All(it => it.ClaudeCode is not null), $"{tabsToComeBack} tabs to run their sessions again");
   }

   /// <summary>Has the sandbox reload Claude Sessions, as for an update, and returns once the reloaded plugin answers.</summary>
   public void ReloadThePlugin() => _sandbox.ReloadThePlugin(OurPlugin.ClaudeSessions);

   JsonNode Project() => Channel.State()["projects"]!.AsArray().Single()!;
}

/// <summary>
///   A tab of the view: the process its terminal runs, its shell; the Claude Code that runs below it, while one does; and
///   the session that runs in it, or ran in it last, with the folder it runs in and its name.
/// </summary>
public sealed record SessionTab(long? ShellProcessId, ClaudeCodeProcess? ClaudeCode, string? SessionId, string? WorkingDirectory, string? SessionName);

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
