using System.Diagnostics;
using System.Text.Json;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;
using RiderTerminalSymbolLinker.DevelopmentTools.Screen;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A second Rider, with settings, caches and logs of its own, that loads this working tree's build of the plugin from
///   the <see cref="GradleSandbox"/>, and runs the .NET Analysis&amp;Refactoring MCP's server on a port of its own,
///   <see cref="McpPort"/>. Claude starts it, verifies the plugin in it and stops it, without touching the Riders
///   Magnus works in.
/// </summary>
/// <remarks>
///   Its folders are under <c>%LOCALAPPDATA%\RiderTerminalSymbolLinkerSandbox</c>, outside the repository, so the IDE
///   that has the repository open doesn't watch its caches change. Rider's launcher reads the folders from the
///   properties file that the <c>RIDER_PROPERTIES</c> environment variable names.<br/>
///   The properties file also sets <c>idea.auto.reload.plugins</c>, so that a running sandbox reloads the plugin when
///   <see cref="Install"/> rebuilds it, without a restart.
/// </remarks>
sealed class SandboxRider
{
   /// <summary>
   ///   The port the .NET Analysis&amp;Refactoring MCP's server listens on in this sandbox, which the plugin calls too.
   ///   Everyday Riders use 23751, and that plugin's own sandbox 23841.
   /// </summary>
   public const int McpPort = 23861;

   static readonly TimeSpan HowLongStartingMayTake = TimeSpan.FromMinutes(5);
   static readonly TimeSpan HowLongStoppingMayTake = TimeSpan.FromMinutes(2);

   static readonly HttpClient HealthClient = new() { Timeout = TimeSpan.FromSeconds(5) };

   readonly RiderInstallation _rider;
   readonly GradleSandbox _plugins;
   readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiderTerminalSymbolLinkerSandbox");

   public SandboxRider(RiderInstallation rider)
   {
      _rider = rider;
      _plugins = new GradleSandbox(rider);
   }

   /// <summary>Where solutions to open in the sandbox go. The sandbox trusts everything under it.</summary>
   public string SolutionsFolder => Path.Combine(_root, "solutions");

   /// <summary>The copy of the fixture solution that the sandbox opens unless it's given another.</summary>
   public string FixtureSolution => Path.Combine(SolutionsFolder, "FixtureSolution", "FixtureSolution.slnx");

   string ProcessFile => Path.Combine(_root, "running-process.txt");

   public bool IsRunning => RunningProcess() is not null;

   /// <summary>
   ///   Builds this working tree's plugin into the <see cref="GradleSandbox"/>. A running sandbox reloads it from there
   ///   by itself; one that isn't loads it when it starts.
   /// </summary>
   public void Install() => _plugins.Fill();

   /// <summary>
   ///   Starts the sandbox unless it's running, and places its windows. It opens <paramref name="solution"/>, or a fresh
   ///   copy of the fixture solution when it starts without one, and returns once the MCP server on
   ///   <see cref="McpPort"/> answers with the solution loaded and indexed.
   /// </summary>
   public void Start(string? solution = null)
   {
      if(RunningProcess() is null)
      {
         if(!_plugins.HasBothPlugins) Install();
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

   /// <summary>The copy replaces the last one, so no run sees what an earlier one changed.</summary>
   void CopyTheFixtureSolution()
   {
      var copy = Path.GetDirectoryName(FixtureSolution)!;
      if(Directory.Exists(copy)) Directory.Delete(copy, recursive: true);
      CopyFolder(RepositoryRoot.FixtureSolutionFolder, copy);

      static void CopyFolder(string source, string destination)
      {
         Directory.CreateDirectory(destination);
         foreach(var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
         foreach(var folder in Directory.GetDirectories(source).Where(it => Path.GetFileName(it) is not ("bin" or "obj")))
            CopyFolder(folder, Path.Combine(destination, Path.GetFileName(folder)));
      }
   }

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
      Environment.SetEnvironmentVariable("DOTNET_ANALYSIS_AND_REFACTORING_MCP_TOOLS", _plugins.McpToolsFolder);
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
                            $"idea.plugins.path={Forward(_plugins.PluginsFolder)}",
                            $"idea.log.path={Forward(_root)}/log",
                            "idea.auto.reload.plugins=true"
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
