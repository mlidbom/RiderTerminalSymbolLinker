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
         WaitUntilTheSolutionIsReady();
      }
      catch(Exception failure) // xUnit doesn't dispose a fixture whose constructor failed, so the sandbox is stopped here, and the failure reported.
      {
         StopTheSandboxAfter(failure);
         throw;
      }
   }

   /// <summary>
   ///   The MCP build the run checks the plugin against: the one Magnus's Riders run, unless the environment variable
   ///   <c>SANDBOX_MCP_BUILD</c> names another <see cref="McpBuild"/>, such as <c>Marketplace</c> before a release.
   /// </summary>
   static McpBuild TheMcpBuildToRun() =>
      Environment.GetEnvironmentVariable("SANDBOX_MCP_BUILD") is {} build ? Enum.Parse<McpBuild>(build, ignoreCase: true) : McpBuild.Everyday;

   /// <summary>
   ///   Stops the sandbox after <paramref name="failure"/> to start it. A failure to stop it too, as when its hub never
   ///   started, is thrown together with <paramref name="failure"/>: thrown alone, it replaced the failure that mattered in
   ///   every fact's message (2026-11-24).
   /// </summary>
   void StopTheSandboxAfter(Exception failure)
   {
      try
      {
         Dispose();
      }
      catch(Exception stopping)
      {
         throw new AggregateException("The sandbox failed to start, and then to stop.", failure, stopping);
      }
   }

   public void Dispose()
   {
      try
      {
         if(_theRunStartedTheSandbox) _sandbox.StopAndEndItsSessions();
      }
      finally
      {
         _turn.Dispose();
      }
   }

   /// <summary>
   ///   Clicks the link whose text is <paramref name="link"/> in <paramref name="line"/> of terminal output, with no editor
   ///   open, so that the click opens its file afresh, as for a file the user hasn't opened. It waits first until the solution
   ///   is ready, as the run's start does: Claude Sessions' specifications restart the sandbox Rider.
   /// </summary>
   public ClickOutcome Click(string line, string link)
   {
      WaitUntilTheSolutionIsReady();
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

   /// <summary>
   ///   Edits <paramref name="file"/>, a path in the fixture solution, in the sandbox's copy of it, as a user saving an edit
   ///   does: writes <paramref name="content"/> to it unless it holds that already, and returns once the MCP finds
   ///   <paramref name="symbolTheContentDeclares"/>, so the edit has been taken in. The copy keeps the edit while the
   ///   sandbox runs.
   /// </summary>
   public void Edit(string file, string content, string symbolTheContentDeclares)
   {
      var path = Path.Combine(FixtureFolder, file);
      if(File.ReadAllText(path) != content) File.WriteAllText(path, content);
      _sandbox.WaitUntilItsMcpDeclares(symbolTheContentDeclares);
   }

   string FixtureFolder => Path.GetDirectoryName(_sandbox.FixtureSolution)!;

   /// <summary>
   ///   Waits until the symbol linker has indexed the fixture solution, and Rider knows where each file the fixture's source
   ///   generator wrote is, which it learns seconds after the solution opens. Its observation channel may not listen yet
   ///   when the sandbox has started, which waits for the MCP server only: a refused connection is waited out too.
   /// </summary>
   void WaitUntilTheSolutionIsReady()
   {
      var deadline = DateTime.UtcNow + HowLongIndexingMayTake;
      JsonNode? state;
      while(!IsReady(state = StateOrNullWhileNotListening()))
      {
         if(DateTime.UtcNow > deadline) throw new TimeoutException($"The fixture solution wasn't ready after {HowLongIndexingMayTake}: {state?.ToJsonString() ?? "the plugin's observation channel didn't listen"}");
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

      static bool IsReady(JsonNode? state) =>
         state?["projects"]!.AsArray().Any(it => it!["symbolIndex"]!["ready"]!.GetValue<bool>() && it["fileIndex"]!["ready"]!.GetValue<bool>() && EveryGeneratedFileIsLocated(it)) == true;

      static bool EveryGeneratedFileIsLocated(JsonNode project) =>
         project["generatedFiles"]!["listed"]!.GetValue<int>() is var listed and > 0 && project["generatedFiles"]!["located"]!.GetValue<int>() == listed;
   }
}
