using System.Diagnostics;
using System.Text.Json.Nodes;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;
using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

/// <summary>
///   The Claude Sessions tool window in the sandbox Rider, as the specifications see and drive it through Claude
///   Sessions' development observation channel. Its tabs run the sandbox's fake Claude Code, <c>tools/FakeClaudeCode</c>.
/// </summary>
/// <remarks>It starts with no tabs, and no sessions saved: every story starts from there.</remarks>
public sealed class TheClaudeSessionsView
{
   /// <summary>How long the view may take to show a change: the keeper looks every two seconds, the fake starts in less.</summary>
   static readonly TimeSpan HowLongAChangeMayTake = TimeSpan.FromSeconds(15);

   readonly SandboxRider _sandbox = new(RiderInstallation.OfThisUser());

   ClaudeSessionsObservationChannel Channel => _sandbox.ClaudeSessionsObservation;

   public TheClaudeSessionsView()
   {
      Channel.OpenTheView();
      Channel.CloseEveryTab();
      WaitUntil(() => Tabs.Count == 0 && SavedSessions.Count == 0, "the view to have no tabs, and no sessions saved");
   }

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
   ///   <c>%LOCALAPPDATA%\ClaudeSessions\ides\&lt;its process id&gt;.json</c>, or null while there is none.
   /// </summary>
   public JsonNode? LocalApiAnnouncement
   {
      get
      {
         var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeSessions", "ides", $"{_sandbox.ProcessId}.json");
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
                                                                            tab["session"]?["workingDirectory"]!.GetValue<string>()))
   ];

   /// <summary>The ids of the sessions saved for the next start, in the order of the tabs that ran them.</summary>
   public IReadOnlyList<string> SavedSessions => [.. Project()["saved"]!["sessions"]!.AsArray().Select(it => it!["id"]!.GetValue<string>())];

   /// <summary>
   ///   The processes the Terminal tool window's tabs run, where the view's tabs are parked while the plugin reloads, or
   ///   null for a tab whose process hasn't started.
   /// </summary>
   public IReadOnlyList<long?> TerminalToolWindowProcesses => [.. Project()["terminalToolWindow"]!["tabs"]!.AsArray().Select(it => it!["processId"]?.GetValue<long>())];

   /// <summary>Starts a new session, as the New Claude Session action does, and returns its tab once the session runs.</summary>
   public SessionTab StartANewSession()
   {
      var tab = Channel.StartANewSession()["tab"]!.GetValue<int>();
      WaitUntil(() => Tabs.Count > tab && Tabs[tab].SessionId is not null, "the new tab to run a session");
      return Tabs[tab];
   }

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
///   the session that runs in it, or ran in it last.
/// </summary>
public sealed record SessionTab(long? ShellProcessId, ClaudeCodeProcess? ClaudeCode, string? SessionId, string? WorkingDirectory);

/// <summary>The process of a Claude Code that runs in a tab, and the process it's a child of.</summary>
public sealed record ClaudeCodeProcess(long ProcessId, long? ParentProcessId);
