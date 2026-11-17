using System.Diagnostics;
using System.Text.Json;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;
using RiderTerminalSymbolLinker.DevelopmentTools.Screen;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A second Rider, with settings, caches, plugins and logs of its own, that runs this working tree's build of the
///   plugin, and the .NET Analysis&amp;Refactoring MCP's server on a port of its own, <see cref="McpPort"/>, from the
///   <see cref="McpBuild"/> it's given. Claude starts it, verifies the plugin in it and stops it, without touching
///   the Riders Magnus works in.
/// </summary>
/// <remarks>
///   Its folders are under <c>%LOCALAPPDATA%\RiderTerminalSymbolLinkerSandbox</c>, outside the repository, so the IDE
///   that has the repository open doesn't watch its caches change. Rider's launcher reads the folders from the
///   properties file that the <c>RIDER_PROPERTIES</c> environment variable names.<br/>
///   The properties file also sets <c>idea.auto.reload.plugins</c>, so that a running sandbox reloads the plugin when
///   <see cref="Install"/> puts a new build in its plugins folder, without a restart.
/// </remarks>
public sealed class SandboxRider
{
   /// <summary>
   ///   The port the .NET Analysis&amp;Refactoring MCP's server listens on in this sandbox, which the plugin calls too.
   ///   Everyday Riders use 23751, and that plugin's own sandbox 23841.
   /// </summary>
   public const int McpPort = 23861;

   /// <summary>
   ///   The port the plugin's development observation channel listens on in this sandbox, and nowhere else. Not
   ///   <see cref="McpPort"/> + 1: Rider's lightweight backend process loads the MCP plugin too, and its server takes
   ///   the next free port.
   /// </summary>
   public const int ObservationPort = 23871;

   /// <summary>The plugin's development observation channel in this sandbox.</summary>
   public ObservationChannel Observation { get; } = new(ObservationPort);

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

   /// <summary>Where solutions to open in the sandbox go. The sandbox trusts everything under it.</summary>
   public string SolutionsFolder => Path.Combine(_root, "solutions");

   /// <summary>The copy of the fixture solution that the sandbox opens unless it's given another.</summary>
   public string FixtureSolution => Path.Combine(SolutionsFolder, RepositoryRoot.FixtureSolutionName, RepositoryRoot.FixtureSolutionName + ".slnx");

   string ProcessFile => Path.Combine(_root, "running-process.txt");
   string PluginsFolder => Path.Combine(_root, "plugins");
   string McpPluginFolder => Path.Combine(PluginsFolder, GradleSandbox.McpPluginFolderName);
   bool HasBothPlugins => Directory.Exists(Path.Combine(PluginsFolder, GradleSandbox.PluginFolderName)) && Directory.Exists(McpPluginFolder);

   public bool IsRunning => RunningProcess() is not null;

   /// <summary>
   ///   Builds this working tree's plugin with Gradle and puts it in the sandbox's plugins folder, with the
   ///   <paramref name="mcp"/> build of the .NET Analysis&amp;Refactoring MCP. A running sandbox is then made to reload
   ///   our plugin, and this returns once the new build answers. Returns what it did.
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
      var mirror = new FolderMirror(Path.Combine(_root, "replaced-plugin-files"));
      var pluginChanged = mirror.Mirror(_plugins.Plugin, Path.Combine(PluginsFolder, GradleSandbox.PluginFolderName));
      if(IsRunning)
      {
         var mcpTools = mcpToolsChanged ? $"Its MCP swapped to the {mcp} tools build {WaitUntilTheMcpServesItsNewestToolsBuild()}. " : "";
         if(!pluginChanged) return mcpTools + "The sandbox has this build of the plugin already.";
         if(!mirror.AddedOrRemovedAFile)
            return mcpTools + $"Put the new build in the running sandbox Rider, which reloaded it in {Observation.ReloadThePlugin(TimeSpan.FromMinutes(1)).TotalSeconds:0.0}s.";

         Stop();
         Start();
         return mcpTools + "Put the new build in the sandbox and restarted it: the build adds or removes a file of the plugin's, which Rider can't reload.";
      }

      mirror.Mirror(mcpPlugin, McpPluginFolder);
      if(whyItRestarts is null) return $"Put the build in the sandbox, with the {mcp} build of the MCP: the sandbox Rider loads them when it starts.";

      Start();
      return $"Put the build in the sandbox with the {mcp} build of the MCP, and restarted it: {whyItRestarts}.";
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
         if(!HasBothPlugins) Install();
         if(solution is null)
         {
            CopyTheFixtureSolution();
            solution = FixtureSolution;
         }

         new SandboxRiderSettings(Path.Combine(_root, "config")).Write(SolutionsFolder);
         WriteTheFoldersFile();
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
      Environment.SetEnvironmentVariable("RIDER_PROPERTIES", Path.Combine(_root, "idea.properties"));
      Environment.SetEnvironmentVariable("DOTNET_ANALYSIS_AND_REFACTORING_MCP_PORT", McpPort.ToString());
      Environment.SetEnvironmentVariable("DOTNET_ANALYSIS_AND_REFACTORING_MCP_TOOLS", _mcpTools.Folder);
      var startInformation = new ProcessStartInfo(_rider.Launcher) { UseShellExecute = true };
      startInformation.ArgumentList.Add(solution);
      return Process.Start(startInformation) ?? throw new InvalidOperationException($"Could not start {_rider.Launcher}.");
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
                            $"dev.magnus.csl.observation.port={ObservationPort}"
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
