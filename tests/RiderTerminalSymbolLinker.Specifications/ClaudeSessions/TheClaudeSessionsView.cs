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

   /// <summary>The view's tabs, in their order.</summary>
   public IReadOnlyList<SessionTab> Tabs =>
   [
      .. Project()["view"]!["tabs"]!.AsArray().Select(tab => new SessionTab(tab!["processId"]?.GetValue<long>(),
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

   /// <summary>Closes the sandbox Rider as a user would, and starts it again, and returns once the view has its tabs.</summary>
   public void RestartTheIde(int tabsToComeBack)
   {
      _sandbox.Stop();
      _sandbox.Start();
      WaitUntil(() => Tabs.Count == tabsToComeBack && Tabs.All(it => it.SessionId is not null), $"{tabsToComeBack} tabs to run their sessions again");
   }

   /// <summary>Has the sandbox reload Claude Sessions, as for an update, and returns once the reloaded plugin answers.</summary>
   public void ReloadThePlugin() => _sandbox.ReloadThePlugin(OurPlugin.ClaudeSessions);

   JsonNode Project() => Channel.State()["projects"]!.AsArray().Single()!;
}

/// <summary>A tab of the view: the process its terminal runs, and the session that runs in it, once it does.</summary>
public sealed record SessionTab(long? ProcessId, string? SessionId, string? WorkingDirectory);
