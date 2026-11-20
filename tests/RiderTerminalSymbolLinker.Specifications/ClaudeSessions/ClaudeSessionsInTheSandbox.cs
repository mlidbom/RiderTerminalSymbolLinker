using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;
using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

/// <summary>
///   Claude Sessions in the sandbox Rider, as the specifications see and drive it: the local API through which other
///   tools see the IDE's windows and restart it, the IDE's restarts, and the sandbox's own Cloyd, which Claude Sessions
///   has show each project's window as the project opens.
/// </summary>
/// <remarks>
///   Every story starts with the fixture solution open, and the sandbox's Cloyd showing its window, as
///   <see cref="SandboxRider.Start"/> leaves them.
/// </remarks>
public sealed class ClaudeSessionsInTheSandbox
{
   static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

   readonly SandboxRider _sandbox = new(RiderInstallation.OfThisUser());

   /// <summary>The fixture solution's folder in the sandbox: its project's folder, as the IDE and Cloyd name it.</summary>
   public string ProjectFolder => Path.GetDirectoryName(_sandbox.FixtureSolution)!;

   /// <summary>The process the sandbox Rider runs as.</summary>
   public int SandboxProcessId => _sandbox.ProcessId;

   /// <summary>The process the sandbox's Cloyd application runs as: null when none runs.</summary>
   public int? CloydsApplication => _sandbox.ItsCloyd.TheApplication()?.Id;

   /// <summary>The windows the sandbox's Cloyd application shows, each with its project's folder and its title: none when none runs.</summary>
   public IReadOnlyList<JsonNode> CloydsWindows => [.. (_sandbox.ItsCloyd.Windows() ?? []).OfType<JsonNode>()];

   /// <summary>The folders of the projects whose windows the sandbox's Cloyd shows, in order: none when no application runs.</summary>
   public IReadOnlyList<string> ProjectsCloydShows => [.. _sandbox.ItsCloyd.ProjectsItShows().Order(StringComparer.OrdinalIgnoreCase)];

   /// <summary>
   ///   Ends the sandbox's Cloyd application, as closing its last window does, then has Claude Sessions start in the
   ///   sandbox Rider again, as for an update of the plugin: it runs its startup again for the project open, as when the
   ///   project opens. Returns once Cloyd shows the project's window.
   /// </summary>
   public void StartClaudeSessionsAgainWithNoCloydRunning()
   {
      _sandbox.ItsCloyd.StopTheApplication();
      _sandbox.ReloadThePlugin(OurPlugin.ClaudeSessions);
      _sandbox.WaitUntilItsCloydShows(ProjectFolder);
   }

   /// <summary>
   ///   Ends the sandbox's Cloyd application, as closing its last window does, then performs Tools &gt; Show the Project in
   ///   Cloyd in the sandbox Rider, as the user would. Returns once Cloyd shows the project's window.
   /// </summary>
   public void ShowTheProjectInCloydFromTheIdeWithNoCloydRunning()
   {
      _sandbox.ItsCloyd.StopTheApplication();
      _sandbox.ClaudeSessionsObservation.Perform(ClaudeSessionsObservationChannel.ShowTheProjectInCloyd);
      _sandbox.WaitUntilItsCloydShows(ProjectFolder);
   }

   /// <summary>
   ///   The file in which the sandbox Rider's Claude Sessions announces its local API to other tools on the machine:
   ///   <c>&lt;its process id&gt;.json</c> in the sandbox's folder of announcements, or null while there is none.
   /// </summary>
   JsonNode? LocalApiAnnouncement
   {
      get
      {
         var file = Path.Combine(_sandbox.LocalApiAnnouncementsFolder, $"{_sandbox.ProcessId}.json");
         return File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) : null;
      }
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
      using var request = new HttpRequestMessage(new HttpMethod(method), $"http://127.0.0.1:{port}{path}");
      if(body is not null) request.Content = JsonContent.Create(body);
      if(withTheToken) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", announcement["token"]!.GetValue<string>());
      if(host is not null) request.Headers.Host = host;
      using var answer = Http.Send(request);
      var text = answer.Content.ReadAsStringAsync().Result;
      return ((int)answer.StatusCode, text.Length > 0 ? JsonNode.Parse(text) : null);
   }

   /// <summary>
   ///   Follows the sandbox Rider through a restart it makes itself, as <c>POST /restart</c> has it do, from the process
   ///   <paramref name="before"/> it ran as, and returns once it runs again with the fixture solution open, and Cloyd shows
   ///   its window.
   /// </summary>
   public void FollowTheIdesRestart(int before)
   {
      _sandbox.FollowItsRestart(before, _sandbox.FixtureSolution);
      _sandbox.WaitUntilItsCloydShows(ProjectFolder);
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
      FollowTheIdesRestart(before);
   }

   /// <summary>
   ///   Has the sandbox's hub restart the sandbox Rider, as Magnus asks it himself, so with no prompt of his, its restarter
   ///   running <paramref name="beforeItStartsAgain"/>, unless null, before it starts it again, as a deploy has it; and
   ///   returns once the IDE runs again and the hub has an outcome: see <see cref="SandboxRider.AskItsHubToRestartTheIde"/>.
   /// </summary>
   public IdeRestartByTheHub HaveTheHubRestartTheIde(string why, IReadOnlyList<string>? beforeItStartsAgain = null)
   {
      var before = SandboxProcessId;
      _sandbox.AskItsHubToRestartTheIde(why, askedBy: "Claude Sessions' specifications", beforeItStartsAgain);
      FollowTheIdesRestart(before);
      WaitUntil(() => _sandbox.TheIdeRestartOfItsHub()?["stage"]?.GetValue<string>() == "done", "the hub to say how the restart went",
                HowLongTheHubMayTakeToSayRiderIsBack);
      var restart = _sandbox.TheIdeRestartOfItsHub()!;
      return new IdeRestartByTheHub(restart["outcome"]!["kind"]!.GetValue<string>(),
                                    restart["outcome"]!["headline"]!.GetValue<string>(),
                                    [.. restart["progress"]!.AsArray().Select(it => it!["text"]!.GetValue<string>())],
                                    File.Exists(_sandbox.IdeRestartsLog) ? File.ReadAllText(_sandbox.IdeRestartsLog) : "");
   }

   /// <summary>How long the hub may take, once the sandbox Rider runs again, to find it back: its projects open and Royder done indexing them.</summary>
   static readonly TimeSpan HowLongTheHubMayTakeToSayRiderIsBack = TimeSpan.FromMinutes(3);

   /// <summary>The folders of the projects whose windows the sandbox Rider shows, as Claude Sessions' <c>GET /windows</c> tells of them, in order.</summary>
   public IReadOnlyList<string> ProjectWindows =>
      [.. AskTheLocalApi("GET", "/windows").Body!["windows"]!.AsArray().Select(it => Path.GetFullPath(it!["project"]!.GetValue<string>())).Order(StringComparer.OrdinalIgnoreCase)];

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
   ///   <paramref name="names"/>: kills the sandbox Rider, ends the sandbox's Cloyd application, which shows their windows
   ///   too, deletes them, so that Rider doesn't open them again, and starts it again with the fixture solution alone.
   ///   Killed, not stopped: closed one window after the other, Rider showed its Rider Home window in place of the last,
   ///   and didn't exit (2026-11-19).
   /// </summary>
   public void CloseTheCopiesOfTheFixtureSolution(params string[] names)
   {
      _sandbox.Kill();
      _sandbox.ItsCloyd.StopTheApplication();
      foreach(var name in names) _sandbox.DeleteTheCopyOfTheFixtureSolution(name);
      _sandbox.Start(_sandbox.FixtureSolution);
   }

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
}
