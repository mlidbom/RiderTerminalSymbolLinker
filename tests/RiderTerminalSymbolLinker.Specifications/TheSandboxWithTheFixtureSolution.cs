using System.Text.Json.Nodes;
using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.Specifications;

/// <summary>
///   The sandbox Rider running this working tree's build of the plugin, with the fixture solution
///   (<c>tests/RiderTerminalSymbolLinker.FixtureSolution</c>) open and the plugin's indexes of it ready. Every
///   specification asks the plugin there, through its development observation channel.
/// </summary>
/// <remarks>
///   Set up once for the whole run. The working tree's build is put in the sandbox first: a running sandbox reloads it,
///   in seconds; otherwise the sandbox starts with it, with a fresh copy of the fixture solution, in about half a
///   minute.<br/>
///   A sandbox the run started is stopped when the run ends: it holds about 4.6 GB, which Magnus's machine needs
///   (Magnus, 2026-11-17: "keep it off when you are not working with it"). One that was running before the run, because
///   <c>sandbox start</c> started it for a stretch of work, is left running, and each run then takes seconds.
/// </remarks>
public sealed class TheSandboxWithTheFixtureSolution : IDisposable
{
   static readonly TimeSpan HowLongIndexingMayTake = TimeSpan.FromMinutes(2);

   readonly SandboxRider _sandbox = new(RiderInstallation.OfThisUser());
   readonly bool _theRunStartedTheSandbox;

   public TheSandboxWithTheFixtureSolution()
   {
      _theRunStartedTheSandbox = !_sandbox.IsRunning;
      _sandbox.Install();
      if(!_sandbox.IsRunning) _sandbox.Start();
      WaitUntilThePluginHasIndexedTheSolution();
   }

   public void Dispose()
   {
      if(_theRunStartedTheSandbox) _sandbox.Stop();
   }

   /// <summary>
   ///   Clicks the link whose text is <paramref name="link"/> in <paramref name="line"/> of terminal output, with no editor
   ///   open, so that the click opens its file afresh, as for a file the user hasn't opened.
   /// </summary>
   public ClickOutcome Click(string line, string link)
   {
      _sandbox.Observation.CloseEveryEditor();
      return ClickOutcome.From(_sandbox.Observation.Click(line, link), FixtureFolder);
   }

   /// <summary>Chooses a row of the picker the last click showed, as Enter does.</summary>
   public ClickOutcome ChoosePickerRow(int row) => ClickOutcome.From(_sandbox.Observation.ChoosePickerRow(row), FixtureFolder);

   /// <summary>Closes the picker the last click showed.</summary>
   public void ClosePicker() => _sandbox.Observation.ClosePicker();

   /// <summary>The texts of the links the plugin makes in <paramref name="line"/>, each with its kind: symbol or file.</summary>
   public IReadOnlyList<(string Text, string Kind)> LinksIn(string line) =>
      [.. _sandbox.Observation.Links(line)["links"]!.AsArray().Select(it => (it!["text"]!.GetValue<string>(), it["kind"]!.GetValue<string>()))];

   string FixtureFolder => Path.GetDirectoryName(_sandbox.FixtureSolution)!;

   void WaitUntilThePluginHasIndexedTheSolution()
   {
      var deadline = DateTime.UtcNow + HowLongIndexingMayTake;
      JsonNode state;
      while(!BothIndexesAreReady(state = _sandbox.Observation.State()))
      {
         if(DateTime.UtcNow > deadline) throw new TimeoutException($"The plugin's indexes of the fixture solution weren't ready after {HowLongIndexingMayTake}: {state}");
         Thread.Sleep(500);
      }

      static bool BothIndexesAreReady(JsonNode state) =>
         state["projects"]!.AsArray().Any(it => it!["symbolIndex"]!["ready"]!.GetValue<bool>() && it["fileIndex"]!["ready"]!.GetValue<bool>());
   }
}
