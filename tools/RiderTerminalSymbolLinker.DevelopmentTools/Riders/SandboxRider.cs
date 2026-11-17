using System.Diagnostics;
using System.Text.Json;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;
using RiderTerminalSymbolLinker.DevelopmentTools.Screen;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A second Rider, with settings, caches, plugins and logs of its own, that runs this working tree's builds of
///   <see cref="OurPlugin.All"/>, and the .NET Analysis&amp;Refactoring MCP's server on a port of its own,
///   <see cref="McpPort"/>, from the <see cref="McpBuild"/> it's given. Claude starts it, verifies the plugins in it and
///   stops it, without touching the Riders Magnus works in.
/// </summary>
/// <remarks>
///   Its folders are under <c>%LOCALAPPDATA%\RiderTerminalSymbolLinkerSandbox</c>, outside the repository, so the IDE
///   that has the repository open doesn't watch its caches change. Rider's launcher reads the folders from the
///   properties file that the <c>RIDER_PROPERTIES</c> environment variable names.<br/>
///   The properties file also sets <c>idea.auto.reload.plugins</c>, so that a running sandbox reloads the plugin when
///   <see cref="Install"/> puts a new build in its plugins folder, without a restart.<br/>
///   Claude Sessions runs in any JetBrains IDE, and is verified in this Rider too: what it uses is the platform's and
///   the terminal's, which every IDE has alike.
/// </remarks>
public sealed class SandboxRider
{
   /// <summary>
   ///   The port the .NET Analysis&amp;Refactoring MCP's server listens on in this sandbox, which the plugin calls too.
   ///   Everyday Riders use 23751, and that plugin's own sandbox 23841.
   /// </summary>
   public const int McpPort = 23861;

   /// <summary>
   ///   The port the symbol linker's development observation channel listens on in this sandbox, and nowhere else. Not
   ///   <see cref="McpPort"/> + 1: Rider's lightweight backend process loads the MCP plugin too, and its server takes
   ///   the next free port.
   /// </summary>
   public const int SymbolLinkerObservationPort = 23871;

   /// <summary>The port Claude Sessions' development observation channel listens on in this sandbox, and nowhere else.</summary>
   public const int ClaudeSessionsObservationPort = 23872;

   /// <summary>The symbol linker's development observation channel in this sandbox.</summary>
   public SymbolLinkerObservationChannel SymbolLinkerObservation { get; } = new(SymbolLinkerObservationPort);

   /// <summary>Claude Sessions' development observation channel in this sandbox.</summary>
   public ClaudeSessionsObservationChannel ClaudeSessionsObservation { get; } = new(ClaudeSessionsObservationPort);

   ObservationChannel ObservationOf(OurPlugin plugin) =>
      plugin == OurPlugin.SymbolLinker ? SymbolLinkerObservation : ClaudeSessionsObservation;

   static readonly TimeSpan HowLongStartingMayTake = TimeSpan.FromMinutes(5);
   static readonly TimeSpan HowLongStoppingMayTake = TimeSpan.FromMinutes(2);

   static readonly HttpClient HealthClient = new() { Timeout = TimeSpan.FromSeconds(5) };

   readonly RiderInstallation _rider;
   readonly GradleSandbox _plugins;
   readonly EverydayRider _everyday;
   readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiderTerminalSymbolLinkerSandbox");

   /// <summary>
   ///   The tools builds the sandbox's MCP plugin serves the newest of, before those its plugin ships with: the
   ///   <see cref="EverydayRider"/>'s, or none.
   /// </summary>
   readonly McpToolsBuilds _mcpTools;

   public SandboxRider(RiderInstallation rider)
   {
      _rider = rider;
      _plugins = new GradleSandbox(rider);
      _everyday = new EverydayRider(rider);
      _mcpTools = new McpToolsBuilds(Path.Combine(_root, "mcp-tools"));
   }

   /// <summary>
   ///   Claude Code's folder in the sandbox, which <c>CLAUDE_CONFIG_DIR</c> names there: where the fake Claude Code
   ///   records the sessions it runs, as Claude Code does in the user's.
   /// </summary>
   public string ClaudeCodeFolder => Path.Combine(_root, "claude-code");

   /// <summary>Where the tools build the fake Claude Code, <c>tools/FakeClaudeCode</c>'s <c>claude.exe</c>.</summary>
   static string FakeClaudeCodeBuild => Path.Combine(RepositoryRoot.Folder, "tools", "FakeClaudeCode", "bin", "claude");

   /// <summary>
   ///   The sandbox's copy of the <see cref="FakeClaudeCodeBuild"/>, first on its <c>PATH</c>, so that Claude Sessions
   ///   runs it there in place of Claude Code. A copy, taken as the sandbox starts: the build's own <c>claude.exe</c>,
   ///   held open by the sandbox's tabs, kept the tools from building while the sandbox ran.
   /// </summary>
   string FakeClaudeCodeFolder => Path.Combine(_root, "fake-claude-code");

   /// <summary>Where solutions to open in the sandbox go. The sandbox trusts everything under it.</summary>
   public string SolutionsFolder => Path.Combine(_root, "solutions");

   /// <summary>The copy of the fixture solution that the sandbox opens unless it's given another.</summary>
   public string FixtureSolution => Path.Combine(SolutionsFolder, RepositoryRoot.FixtureSolutionName, RepositoryRoot.FixtureSolutionName + ".slnx");

   string ProcessFile => Path.Combine(_root, "running-process.txt");
   string PluginsFolder => Path.Combine(_root, "plugins");
   string McpPluginFolder => Path.Combine(PluginsFolder, GradleSandbox.McpPluginFolderName);
   bool HasEveryPlugin => OurPlugin.All.All(it => Directory.Exists(Path.Combine(PluginsFolder, it.Name))) && Directory.Exists(McpPluginFolder);

   public bool IsRunning => RunningProcess() is not null;

   /// <summary>
   ///   Builds this working tree's plugins with Gradle and puts them in the sandbox's plugins folder, with the
   ///   <paramref name="mcp"/> build of the .NET Analysis&amp;Refactoring MCP. A running sandbox is then made to reload
   ///   those of our plugins that changed, and this returns once their new builds answer. Returns what it did.
   /// </summary>
   /// <remarks>
   ///   The MCP comes in two parts. Its plugin is held open by a running Rider, which can't reload it, so a running
   ///   sandbox is stopped first when the plugin is to change, and started again afterwards. Its
   ///   <see cref="McpToolsBuilds"/> it swaps to at its next call.<br/>
   ///   A running sandbox is restarted too when the fixture solution changed since it copied it, so that it opens a
   ///   fresh copy.
   /// </remarks>
   public string Install(McpBuild mcp = McpBuild.Everyday)
   {
      _plugins.Fill();
      var mcpPlugin = TheMcpPlugin(mcp);
      var whyItRestarts = IsRunning ? WhyTheRunningSandboxMustRestart(mcpPlugin) : null;
      if(whyItRestarts is not null) Stop();

      var mcpToolsChanged = mcp == McpBuild.Everyday ? _mcpTools.TakeTheBuildsOf(_everyday.McpToolsBuilds) : _mcpTools.Clear();
      var startedBefore = OurPlugin.All.ToDictionary(it => it, it => IsRunning ? ObservationOf(it).StartedAt() : null);
      var mirror = new FolderMirror(Path.Combine(_root, "replaced-plugin-files"));
      var changed = new List<OurPlugin>();
      var registrationsChanged = false;
      foreach(var plugin in OurPlugin.All)
      {
         var pluginFolder = Path.Combine(PluginsFolder, plugin.Name);
         var registeredBefore = PluginRegistrations.InPluginFolder(pluginFolder);
         if(mirror.Mirror(_plugins.BuildOf(plugin), pluginFolder)) changed.Add(plugin);
         registrationsChanged |= PluginRegistrations.InPluginFolder(pluginFolder) != registeredBefore;
      }

      if(IsRunning)
      {
         var mcpTools = mcpToolsChanged ? $"Its MCP swapped to the {mcp} tools build {WaitUntilTheMcpServesItsNewestToolsBuild()}. " : "";
         if(changed.Count == 0) return mcpTools + "The sandbox has these builds of the plugins already.";
         if(!mirror.AddedOrRemovedAFile && !registrationsChanged)
         {
            ObservationOf(changed[0]).RefreshThePluginsFolder();
            var reloads = changed.Select(it => $"{it} in {ObservationOf(it).WaitUntilReloaded(startedBefore[it], TimeSpan.FromMinutes(1)).TotalSeconds:0.0}s");
            return mcpTools + $"Put the new builds in the running sandbox Rider, which reloaded {string.Join(" and ", reloads)}.";
         }

         Stop();
         Start();
         return mcpTools + $"Put the new builds of {string.Join(" and ", changed)} in the sandbox and restarted it: a build adds or removes a file of a plugin's, or changes what its plugin.xml registers, which a reload doesn't take.";
      }

      mirror.Mirror(mcpPlugin, McpPluginFolder);
      if(whyItRestarts is null) return $"Put the builds in the sandbox, with the {mcp} build of the MCP: the sandbox Rider loads them when it starts.";

      Start();
      return $"Put the builds in the sandbox with the {mcp} build of the MCP, and restarted it: {whyItRestarts}.";
   }

   /// <summary>Why the running sandbox can't take this install without a restart, or null when it can.</summary>
   string? WhyTheRunningSandboxMustRestart(string mcpPlugin) =>
      !FolderMirror.HoldTheSame(mcpPlugin, McpPluginFolder) ? "a running Rider can't reload the MCP's plugin"
    : File.Exists(FixtureCopiedFromFile) && File.ReadAllText(FixtureCopiedFromFile) != TheFixtureSolutionsFingerprint() ? "the fixture solution changed, and a fresh copy of it opens when the sandbox starts"
    : null;

   /// <summary>Where the <paramref name="build"/> of the .NET Analysis&amp;Refactoring MCP's plugin is.</summary>
   string TheMcpPlugin(McpBuild build)
   {
      var folder = build switch
      {
         McpBuild.Everyday    => _everyday.McpPlugin,
         McpBuild.Marketplace => _plugins.McpPlugin,
         _                    => throw new ArgumentOutOfRangeException(nameof(build), build, null)
      };
      return Directory.Exists(folder) ? folder : throw new InvalidOperationException($"There is no {build} build of the MCP's plugin at {folder}.");
   }

   /// <summary>
   ///   Waits until the running sandbox's MCP serves the newest of <see cref="_mcpTools"/> built against its host, which
   ///   it notices within 2 seconds, and returns the build's name.
   /// </summary>
   string WaitUntilTheMcpServesItsNewestToolsBuild()
   {
      var served = "";
      WaitUntil(() =>
                {
                   if(TheMcpServersHealth() is not {} health) return false;
                   using var document = JsonDocument.Parse(health);
                   var tools = document.RootElement.GetProperty("solutions").EnumerateArray().First().GetProperty("tools");
                   served = tools.GetProperty("build").GetString()!;
                   return served == (_mcpTools.NewestBuiltAgainst(tools.GetProperty("host").GetString()!) ?? served);
                },
                TimeSpan.FromSeconds(30),
                $"the MCP server on port {McpPort} to serve the newest tools build in {_mcpTools.Folder}");
      return served;
   }

   /// <summary>
   ///   Starts the sandbox unless it's running, and places its windows. It opens <paramref name="solution"/>, or a fresh
   ///   copy of the fixture solution when it starts without one, and returns once the MCP server on
   ///   <see cref="McpPort"/> answers with the solution loaded and indexed.
   /// </summary>
   public void Start(string? solution = null)
   {
      if(RunningProcess() is null)
      {
         if(!HasEveryPlugin) Install();
         if(solution is null)
         {
            CopyTheFixtureSolution();
            solution = FixtureSolution;
         }

         new SandboxRiderSettings(Path.Combine(_root, "config")).Write(SolutionsFolder);
         WriteTheFoldersFile();
         TakeTheFakeClaudeCodeBuild();
         var process = Launch(solution);
         File.WriteAllText(ProcessFile, $"{process.Id};{process.StartTime.Ticks}");
      } else if(solution is not null)
      {
         Launch(solution); // A second launch hands the solution to the running sandbox and exits.
      }

      var running = RunningProcess() ?? throw new InvalidOperationException("The sandbox Rider exited while starting. See its logs in " + Path.Combine(_root, "log"));
      WaitUntil(() => FramesOf(running).Count > 0, HowLongStartingMayTake, "the sandbox Rider to show a window");
      PlaceWindows();
      if(solution is null) return;

      WaitUntil(() => TheMcpServerHasIndexed(solution), HowLongStartingMayTake, $"the MCP server on port {McpPort} to answer with {solution} loaded and indexed");
      PlaceWindows(); // The solution's own window has replaced Rider Home by now.
   }

   /// <summary>
   ///   Has the running sandbox reload <paramref name="plugin"/> as it is, as it does a new build, and returns once the
   ///   reloaded plugin answers.
   /// </summary>
   public void ReloadThePlugin(OurPlugin plugin)
   {
      var observation = ObservationOf(plugin);
      var startedBefore = observation.StartedAt();
      foreach(var jar in Directory.GetFiles(Path.Combine(PluginsFolder, plugin.Name, "lib"), "*.jar")) File.SetLastWriteTimeUtc(jar, DateTime.UtcNow);
      observation.RefreshThePluginsFolder();
      observation.WaitUntilReloaded(startedBefore, TimeSpan.FromMinutes(1));
   }

   void TakeTheFakeClaudeCodeBuild()
   {
      if(Directory.Exists(FakeClaudeCodeFolder)) Directory.Delete(FakeClaudeCodeFolder, recursive: true);
      Directory.CreateDirectory(FakeClaudeCodeFolder);
      foreach(var file in Directory.GetFiles(FakeClaudeCodeBuild)) File.Copy(file, Path.Combine(FakeClaudeCodeFolder, Path.GetFileName(file)));
   }

   /// <summary>Closes the sandbox's windows until it exits, as a human closing them would.</summary>
   public void Stop()
   {
      var running = RunningProcess();
      if(running is null) return;
      WaitUntil(() =>
                {
                   if(running.HasExited) return true;
                   foreach(var window in FramesOf(running)) window.AskToClose();
                   return running.WaitForExit(TimeSpan.FromSeconds(5));
                },
                HowLongStoppingMayTake,
                "the sandbox Rider to exit");
      File.Delete(ProcessFile);
   }

   /// <summary>
   ///   Whether the sandbox runs, where its windows are next to the development IDEA's, and what the MCP server's health
   ///   endpoint answers.
   /// </summary>
   public IReadOnlyList<string> Status()
   {
      if(RunningProcess() is not {} running) return ["The sandbox Rider isn't running."];

      return ScreenThread.Run(() => (IReadOnlyList<string>)
                              [
                                 $"The sandbox Rider runs as process {running.Id}.",
                                 .. FramesOf(running).Select(it => "  sandbox window: " + it.Description),
                                 "  development IDEA window: " + (SandboxWindowPlacement.TheDevelopmentIdeaWindow()?.Description ?? "none open"),
                                 "  MCP health: " + (TheMcpServersHealth() ?? "nothing listens on port " + McpPort)
                              ]);
   }

   /// <summary>Puts the sandbox's windows where <see cref="SandboxWindowPlacement"/> says.</summary>
   public void PlaceWindows()
   {
      var running = RunningProcess() ?? throw new InvalidOperationException("The sandbox Rider isn't running.");
      SandboxWindowPlacement.Apply(FramesOf(running));
   }

   /// <summary>
   ///   Where the copy of the fixture solution records <see cref="TheFixtureSolutionsFingerprint"/> as it was copied.
   /// </summary>
   string FixtureCopiedFromFile => Path.Combine(_root, "fixture-solution-copied-from.txt");

   /// <summary>The copy replaces the last one, so no run sees what an earlier one changed.</summary>
   void CopyTheFixtureSolution()
   {
      var copy = Path.GetDirectoryName(FixtureSolution)!;
      if(Directory.Exists(copy)) Directory.Delete(copy, recursive: true);
      CopyFolder(RepositoryRoot.FixtureSolutionFolder, copy);
      File.WriteAllText(FixtureCopiedFromFile, TheFixtureSolutionsFingerprint());

      static void CopyFolder(string source, string destination)
      {
         Directory.CreateDirectory(destination);
         foreach(var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
         foreach(var folder in Directory.GetDirectories(source).Where(it => Path.GetFileName(it) is not ("bin" or "obj")))
            CopyFolder(folder, Path.Combine(destination, Path.GetFileName(folder)));
      }
   }

   /// <summary>
   ///   Each file of the fixture solution in the repository, with its size and time, apart from what builds put in
   ///   <c>bin</c> and <c>obj</c>: it changes when the fixture does.
   /// </summary>
   static string TheFixtureSolutionsFingerprint() =>
      string.Join(Environment.NewLine,
                  Directory.GetFiles(RepositoryRoot.FixtureSolutionFolder, "*", SearchOption.AllDirectories)
                           .Select(it => Path.GetRelativePath(RepositoryRoot.FixtureSolutionFolder, it))
                           .Where(it => !it.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
                           .Order(StringComparer.OrdinalIgnoreCase)
                           .Select(it => $"{it} {new FileInfo(Path.Combine(RepositoryRoot.FixtureSolutionFolder, it)).Length} {File.GetLastWriteTimeUtc(Path.Combine(RepositoryRoot.FixtureSolutionFolder, it)).Ticks}"));

   /// <remarks>
   ///   Through the shell, so Rider gets none of this process's handles. Started directly, it inherited the console
   ///   handles: Rider's output came out in the tool's, and a pipe the tool's output went into stayed open as long as
   ///   Rider ran. The shell can't take environment variables for the one start, so they're set on this process, whose
   ///   environment the started one inherits.
   /// </remarks>
   Process Launch(string solution)
   {
      LeaveOutTheClaudeCodeSessionThisRunsIn();
      Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", ClaudeCodeFolder);
      Environment.SetEnvironmentVariable("PATH", FakeClaudeCodeFolder + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
      Environment.SetEnvironmentVariable("RIDER_PROPERTIES", Path.Combine(_root, "idea.properties"));
      Environment.SetEnvironmentVariable("DOTNET_ANALYSIS_AND_REFACTORING_MCP_PORT", McpPort.ToString());
      Environment.SetEnvironmentVariable("DOTNET_ANALYSIS_AND_REFACTORING_MCP_TOOLS", _mcpTools.Folder);
      var startInformation = new ProcessStartInfo(_rider.Launcher) { UseShellExecute = true };
      startInformation.ArgumentList.Add(solution);
      return Process.Start(startInformation) ?? throw new InvalidOperationException($"Could not start {_rider.Launcher}.");
   }

   /// <summary>
   ///   Removes from this process's environment, which the sandbox inherits, the variables that the Claude Code session
   ///   these tools run in sets for its child processes: a Claude Code that Claude Sessions starts in the sandbox would
   ///   otherwise take itself for a child of that session, and neither record its session nor save its transcript. Those
   ///   set for the user or the machine stay, as an IDE started from the desktop has them.
   /// </summary>
   static void LeaveOutTheClaudeCodeSessionThisRunsIn()
   {
      foreach(var name in Environment.GetEnvironmentVariables().Keys.Cast<string>())
         if(name.StartsWith("CLAUDE", StringComparison.OrdinalIgnoreCase)
         && Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) is null
         && Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) is null)
            Environment.SetEnvironmentVariable(name, null);
   }

   void WriteTheFoldersFile()
   {
      static string Forward(string path) => path.Replace('\\', '/');
      Directory.CreateDirectory(_root);
      File.WriteAllLines(Path.Combine(_root, "idea.properties"),
                         [
                            $"idea.config.path={Forward(_root)}/config",
                            $"idea.system.path={Forward(_root)}/system",
                            $"idea.plugins.path={Forward(PluginsFolder)}",
                            $"idea.log.path={Forward(_root)}/log",
                            "idea.auto.reload.plugins=true",
                            $"dev.magnus.csl.observation.port={SymbolLinkerObservationPort}",
                            $"dev.magnus.claudesessions.observation.port={ClaudeSessionsObservationPort}"
                         ]);
   }

   /// <summary>The sandbox's frontend process, which owns its windows, unless it has exited.</summary>
   Process? RunningProcess()
   {
      if(!File.Exists(ProcessFile)) return null;
      var recorded = File.ReadAllText(ProcessFile).Split(';');
      var process = Process.GetProcesses().FirstOrDefault(it => it.Id == int.Parse(recorded[0]));
      return process is not null && process.StartTime.Ticks == long.Parse(recorded[1]) ? process : null;
   }

   /// <summary>The sandbox's windows apart from its splash screen.</summary>
   static IReadOnlyList<TopLevelWindow> FramesOf(Process process) =>
      [.. TopLevelWindow.AllVisible().Where(it => it.ProcessId == process.Id && it.Title.Length > 0 && it.Title != "splash")];

   static bool TheMcpServerHasIndexed(string solution)
   {
      if(TheMcpServersHealth() is not {} health) return false;
      using var document = JsonDocument.Parse(health);
      return document.RootElement.GetProperty("solutions").EnumerateArray()
                     .Any(it => string.Equals(it.GetProperty("path").GetString(), solution, StringComparison.OrdinalIgnoreCase)
                             && it.TryGetProperty("indexing", out var indexing) && !indexing.GetBoolean());
   }

   /// <summary>What the MCP server's <c>/health</c> endpoint answers, or null while nothing listens on the port.</summary>
   /// <remarks>The server listens only while a solution is open.</remarks>
   static string? TheMcpServersHealth()
   {
      try
      {
         return HealthClient.GetStringAsync($"http://127.0.0.1:{McpPort}/health").GetAwaiter().GetResult();
      }
      catch(HttpRequestException exception) when(exception.HttpRequestError == HttpRequestError.ConnectionError)
      {
         return null;
      }
   }

   static void WaitUntil(Func<bool> condition, TimeSpan timeout, string whatItWaitsFor)
   {
      var stopwatch = Stopwatch.StartNew();
      while(!condition())
      {
         if(stopwatch.Elapsed > timeout) throw new TimeoutException($"Gave up after {timeout} waiting for {whatItWaitsFor}.");
         Thread.Sleep(500);
      }
   }
}
