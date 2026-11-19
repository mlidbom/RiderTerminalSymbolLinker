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
///   <c>sandbox start</c> started it for a stretch of work, is left running, and each run then takes seconds.<br/>
///   The run holds the machine's <see cref="SandboxTurn"/> from before it installs the build until its sandbox has
///   stopped, so it waits while another suite of sandbox Riders runs, such as the .NET Analysis&amp;Refactoring MCP's.
/// </remarks>
public sealed class TheSandboxWithTheFixtureSolution : IDisposable
{
   static readonly TimeSpan HowLongIndexingMayTake = TimeSpan.FromMinutes(2);

   readonly SandboxRider _sandbox = new(RiderInstallation.OfThisUser());
   readonly SandboxTurn _turn;
   readonly bool _theRunStartedTheSandbox;

   public TheSandboxWithTheFixtureSolution()
   {
      _turn = _sandbox.TakeTheTurn("the specifications");
      _theRunStartedTheSandbox = !_sandbox.IsRunning;
      try
      {
         _sandbox.Install(TheMcpBuildToRun());
         if(!_sandbox.IsRunning) _sandbox.Start();
         WaitUntilThePluginHasIndexedTheSolution();
      }
      catch // xUnit doesn't dispose a fixture whose constructor failed, so the sandbox is stopped here, and the failure reported.
      {
         Dispose();
         throw;
      }
   }

   /// <summary>
   ///   The MCP build the run checks the plugin against: the one Magnus's Riders run, unless the environment variable
   ///   <c>SANDBOX_MCP_BUILD</c> names another <see cref="McpBuild"/>, such as <c>Marketplace</c> before a release.
   /// </summary>
   static McpBuild TheMcpBuildToRun() =>
      Environment.GetEnvironmentVariable("SANDBOX_MCP_BUILD") is {} build ? Enum.Parse<McpBuild>(build, ignoreCase: true) : McpBuild.Everyday;

   public void Dispose()
   {
      try
      {
         if(_theRunStartedTheSandbox) _sandbox.Stop();
      }
      finally
      {
         _turn.Dispose();
      }
   }

   /// <summary>
   ///   Clicks the link whose text is <paramref name="link"/> in <paramref name="line"/> of terminal output, with no editor
   ///   open, so that the click opens its file afresh, as for a file the user hasn't opened.
   /// </summary>
   public ClickOutcome Click(string line, string link)
   {
      _sandbox.SymbolLinkerObservation.CloseEveryEditor();
      return ClickOutcome.From(_sandbox.SymbolLinkerObservation.Click(line, link), FixtureFolder);
   }

   /// <summary>Chooses a row of the picker the last click showed, as Enter does.</summary>
   public ClickOutcome ChoosePickerRow(int row) => ClickOutcome.From(_sandbox.SymbolLinkerObservation.ChoosePickerRow(row), FixtureFolder);

   /// <summary>Closes the picker the last click showed.</summary>
   public void ClosePicker() => _sandbox.SymbolLinkerObservation.ClosePicker();

   /// <summary>The texts of the links the plugin makes in <paramref name="line"/>, each with its kind: symbol or file.</summary>
   public IReadOnlyList<(string Text, string Kind)> LinksIn(string line) =>
      [.. _sandbox.SymbolLinkerObservation.Links(line)["links"]!.AsArray().Select(it => (it!["text"]!.GetValue<string>(), it["kind"]!.GetValue<string>()))];

   string FixtureFolder => Path.GetDirectoryName(_sandbox.FixtureSolution)!;

   /// <summary>
   ///   Waits until the symbol linker has indexed the fixture solution. Its observation channel may not listen yet when
   ///   the sandbox has started, which waits for the MCP server only: a refused connection is waited out too.
   /// </summary>
   void WaitUntilThePluginHasIndexedTheSolution()
   {
      var deadline = DateTime.UtcNow + HowLongIndexingMayTake;
      JsonNode? state;
      while(!BothIndexesAreReady(state = StateOrNullWhileNotListening()))
      {
         if(DateTime.UtcNow > deadline) throw new TimeoutException($"The plugin's indexes of the fixture solution weren't ready after {HowLongIndexingMayTake}: {state?.ToJsonString() ?? "its observation channel didn't listen"}");
         Thread.Sleep(500);
      }

      JsonNode? StateOrNullWhileNotListening()
      {
         try
         {
            return _sandbox.SymbolLinkerObservation.State();
         }
         catch(HttpRequestException exception) when(exception.HttpRequestError == HttpRequestError.ConnectionError)
         {
            return null;
         }
      }

      static bool BothIndexesAreReady(JsonNode? state) =>
         state?["projects"]!.AsArray().Any(it => it!["symbolIndex"]!["ready"]!.GetValue<bool>() && it["fileIndex"]!["ready"]!.GetValue<bool>()) == true;
   }
}
