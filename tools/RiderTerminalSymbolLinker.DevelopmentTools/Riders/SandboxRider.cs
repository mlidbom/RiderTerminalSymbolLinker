using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeTooling.Hub.LocalApi.Contract.Sandboxes;
using RiderTerminalSymbolLinker.DevelopmentTools.Cloyd;
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
///   properties file that the <c>RIDER_PROPERTIES</c> environment variable names, and its JVM options from a file of
///   its own too, the <see cref="JvmOptionsFile"/>.<br/>
///   The properties file also sets <c>idea.auto.reload.plugins</c>, so that a running sandbox reloads the plugin when
///   <see cref="Install"/> puts a new build in its plugins folder, without a restart.<br/>
///   Claude Sessions runs in any JetBrains IDE, and is verified in this Rider too: what it uses is the platform's,
///   which every IDE has alike. It has a Cloyd of its own show the projects' sessions, <see cref="ItsCloyd"/>.
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

   static readonly HttpClient McpServerClient = new() { Timeout = TimeSpan.FromSeconds(5) };

   readonly RiderInstallation _rider;
   readonly GradleSandbox _plugins;
   readonly EverydayRider _everyday;
   readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiderTerminalSymbolLinkerSandbox");

   /// <summary>
   ///   The tools builds the sandbox's MCP plugin serves the newest of, before those its plugin ships with: the
   ///   <see cref="EverydayRider"/>'s, or none.
   /// </summary>
   readonly McpToolsBuilds _mcpTools;

   /// <summary>The hub the sandbox's Cloyd follows, which serves the sandbox's <see cref="ClaudeCodeFolder"/>.</summary>
   readonly SandboxHub _hub;

   /// <summary>Where the parts of <see cref="_hub"/> are, as it said when this process last had it run: null until then.</summary>
   SandboxHubStarted? _hubStarted;

   /// <summary>How long the sandbox's Cloyd may take to show a project's window once the project has opened.</summary>
   static readonly TimeSpan HowLongCloydMayTakeToShowAProject = TimeSpan.FromMinutes(1);

   public SandboxRider(RiderInstallation rider)
   {
      _rider = rider;
      _plugins = new GradleSandbox();
      _everyday = new EverydayRider(rider);
      _mcpTools = new McpToolsBuilds(Path.Combine(_root, "mcp-tools"));
      _hub = new SandboxHub(Path.Combine(_root, "hub"));
      ItsCloyd = new InstalledCloyd(Path.Combine(_root, "cloyd"));
   }

   /// <summary>
   ///   The sandbox's own Cloyd, which <c>CLOYD_FOLDER</c> names there: Claude Sessions in the sandbox Rider runs its
   ///   <c>Cloyd.exe --project</c> as a project opens, and its application shows the projects' sessions, with the
   ///   sandbox's hub, apart from Magnus's Cloyd. It outlives the sandbox Rider, as Magnus's outlives his IDEs, and stops
   ///   with its hub (<see cref="StopAndEndItsSessions"/>).
   /// </summary>
   public InstalledCloyd ItsCloyd { get; }

   /// <summary>
   ///   Claude Code's folder in the sandbox, which its hub serves, and <c>CLAUDE_CONFIG_DIR</c> names there: where the
   ///   stand-in for Claude Code records the sessions it runs, as Claude Code does in the user's.
   /// </summary>
   string ClaudeCodeFolder => ItsHub.ClaudeCodeFolder;

   /// <summary>
   ///   Where the sandbox's Claude Sessions announces its local API to other tools, and finds other IDEs' announcements:
   ///   a folder of its own, so that the user's tools and IDEs don't take the sandbox for one of theirs.
   /// </summary>
   public string LocalApiAnnouncementsFolder => Path.Combine(_root, "local-api-announcements");

   /// <summary>Where solutions to open in the sandbox go. The sandbox trusts everything under it.</summary>
   public string SolutionsFolder => Path.Combine(_root, "solutions");

   /// <summary>The copy of the fixture solution that the sandbox opens unless it's given another.</summary>
   public string FixtureSolution => Path.Combine(SolutionsFolder, RepositoryRoot.FixtureSolutionName, RepositoryRoot.FixtureSolutionName + ".slnx");

   string ProcessFile => Path.Combine(_root, "running-process.txt");
   string PluginsFolder => Path.Combine(_root, "plugins");
   string McpPluginFolder => Path.Combine(PluginsFolder, GradleSandbox.McpPluginFolderName);
   bool HasEveryPlugin => OurPlugin.All.All(it => Directory.Exists(Path.Combine(PluginsFolder, it.Name))) && Directory.Exists(McpPluginFolder) && ItsCloyd.IsInstalled;

   public bool IsRunning => RunningProcess() is not null;

   /// <summary>
   ///   Takes the machine's <see cref="SandboxTurn"/> for <paramref name="whatFor"/> in this repository, such as the
   ///   specifications, and waits for it while another suite of sandbox Riders runs.
   /// </summary>
   public SandboxTurn TakeTheTurn(string whatFor) => SandboxTurn.Take($"{whatFor} in {RepositoryRoot.Folder}");

   /// <summary>The process the running sandbox Rider runs as.</summary>
   public int ProcessId => (RunningProcess() ?? throw new InvalidOperationException("The sandbox Rider isn't running.")).Id;

   /// <summary>
   ///   Builds this working tree's plugins with Gradle and puts them in the sandbox's plugins folder, with the
   ///   <paramref name="mcp"/> build of the .NET Analysis&amp;Refactoring MCP, and in <see cref="ItsCloyd"/> the build of
   ///   Cloyd that Magnus's IDEs run, <see cref="InstalledCloyd.Everyday"/>'s, which <c>~/.claude</c>'s
   ///   <c>src/publish.ps1</c> installs: Cloyd moved there on 2026-11-23. A running sandbox is then made to reload those of our plugins that changed, and this
   ///   returns once their new builds answer. Returns what it did.
   /// </summary>
   /// <remarks>
   ///   The MCP comes in two parts. Its plugin is held open by a running Rider, which can't reload it, so a running
   ///   sandbox is stopped first when the plugin is to change, and started again afterwards. Its
   ///   <see cref="McpToolsBuilds"/> it swaps to at its next call.<br/>
   ///   A running sandbox is restarted too when the fixture solution changed since it copied it, so that it opens a
   ///   fresh copy.<br/>
   ///   A running sandbox's <see cref="SandboxHub"/> is made to run on the published hub's build, as a new one may have
   ///   been published since.<br/>
   ///   A new build of Cloyd ends the application that runs the old one, and Claude Sessions, reloaded, starts the new one
   ///   as it shows the projects again: a reload runs its startup again.
   /// </remarks>
   public string Install(McpBuild mcp = McpBuild.Everyday)
   {
      _plugins.Fill();
      if(!InstalledCloyd.Everyday.IsInstalled)
         throw new InvalidOperationException($"No Cloyd is installed in {InstalledCloyd.Everyday.Bin}, for the sandbox to run too: ~/.claude's src/publish.ps1 installs it.");
      var cloyd = InstalledCloyd.Everyday.Bin;
      var cloydChanged = !ItsCloyd.Has(cloyd);
      if(cloydChanged)
      {
         ItsCloyd.StopTheApplication();
         ItsCloyd.Install(cloyd);
      }

      if(IsRunning) RunTheHub();
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
         if(changed.Count == 0 && cloydChanged)
         {
            ReloadThePlugin(OurPlugin.ClaudeSessions);
            WaitUntilItsCloydShowsTheProjects();
            return mcpTools + "The sandbox has these builds of the plugins already. Put Cloyd's new build in, and had Claude Sessions show the projects in it again.";
         }
         if(changed.Count == 0) return mcpTools + "The sandbox has these builds of the plugins and of Cloyd already.";
         if(!mirror.AddedOrRemovedAFile && !registrationsChanged)
         {
            ObservationOf(changed[0]).RefreshThePluginsFolder();
            var reloads = changed.Select(it => $"{it} in {ObservationOf(it).WaitUntilReloaded(startedBefore[it], TimeSpan.FromMinutes(1)).TotalSeconds:0.0}s").ToList();
            if(changed.Contains(OurPlugin.ClaudeSessions)) WaitUntilItsCloydShowsTheProjects();
            return mcpTools + $"Put the new builds in the running sandbox Rider, which reloaded {string.Join(" and ", reloads)}{(cloydChanged ? ", and Cloyd's new build" : "")}.";
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
                   var solutions = document.RootElement.GetProperty("solutions").EnumerateArray().ToList();
                   if(solutions.Count == 0) return false; //A server that has no solution open yet serves no tools build yet.
                   var tools = solutions[0].GetProperty("tools");
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
   ///   <see cref="McpPort"/> answers with the solution loaded and indexed, and <see cref="ItsCloyd"/> shows its window. A
   ///   fresh copy ends the sessions that ran in the one it replaces, which outlive the sandbox Rider in their session
   ///   hosts, their shells in its folder.
   /// </summary>
   public void Start(string? solution = null)
   {
      if(RunningProcess() is null)
      {
         if(!HasEveryPlugin) Install();
         RunTheHub();
         if(solution is null)
         {
            EndItsSessions();
            CopyTheFixtureSolution();
            solution = FixtureSolution;
         }

         new SandboxRiderSettings(Path.Combine(_root, "config")).Write(SolutionsFolder);
         WriteTheFoldersFile();
         WriteTheJvmOptionsFile();
         var process = Launch(solution);
         Record(process);
      } else if(solution is not null)
      {
         Launch(solution); // A second launch hands the solution to the running sandbox and exits.
      }

      var running = RunningProcess() ?? throw new InvalidOperationException("The sandbox Rider exited while starting. See its logs in " + Path.Combine(_root, "log"));
      WaitUntil(() => FramesOf(running).Count > 0, HowLongStartingMayTake, "the sandbox Rider to show a window");
      PlaceWindows();
      if(solution is null) return;

      WaitUntil(() => TheMcpServerHasIndexed(solution), HowLongStartingMayTake, $"the MCP server on port {McpPort} to answer with {solution} loaded and indexed");
      WaitUntilItsCloydShows(Path.GetDirectoryName(solution)!);
      PlaceWindows(); // The solution's own window has replaced Rider Home by now.
   }

   /// <summary>
   ///   Waits until <see cref="ItsCloyd"/> shows the window of the project in <paramref name="projectFolder"/>, as Claude
   ///   Sessions has it do as the project opens, and places it with the sandbox's windows.
   /// </summary>
   public void WaitUntilItsCloydShows(string projectFolder)
   {
      WaitUntil(() => ItsCloyd.ProjectsItShows().Contains(Path.GetFullPath(projectFolder), StringComparer.OrdinalIgnoreCase),
                HowLongCloydMayTakeToShowAProject,
                $"the sandbox's Cloyd, in {ItsCloyd.Folder}, to show the window of {projectFolder}: Claude Sessions runs its Cloyd.exe --project as the project opens, and logs what came of it to the sandbox's idea.log");
      PlaceWindows();
   }

   /// <summary>
   ///   Waits until the MCP server on <see cref="McpPort"/> finds a declaration of <paramref name="qualifiedName"/>, such
   ///   as one that an edit of the open solution's files has just added: the server takes such an edit in within seconds.
   /// </summary>
   public void WaitUntilItsMcpDeclares(string qualifiedName) =>
      WaitUntil(() => TheMcpServerDeclares(qualifiedName), TimeSpan.FromSeconds(30), $"the MCP server on port {McpPort} to find a declaration of {qualifiedName}");

   /// <summary>Waits until <see cref="ItsCloyd"/> shows the window of every project open in the sandbox Rider, and places them.</summary>
   void WaitUntilItsCloydShowsTheProjects()
   {
      foreach(var project in ClaudeSessionsObservation.State()["projects"]!.AsArray()) WaitUntilItsCloydShows(project!["basePath"]!.GetValue<string>());
   }

   /// <summary>
   ///   Has the running sandbox reload <paramref name="plugin"/> as it is, as it does a new build, and returns once the
   ///   reloaded plugin answers.
   /// </summary>
   public void ReloadThePlugin(OurPlugin plugin)
   {
      var observation = ObservationOf(plugin);
      var startedBefore = observation.StartedAt();
      plugin.MakeItLookChangedIn(PluginsFolder);
      observation.RefreshThePluginsFolder();
      observation.WaitUntilReloaded(startedBefore, TimeSpan.FromMinutes(1));
   }

   /// <summary>
   ///   Has the <see cref="SandboxHub"/> run, in the sandbox's environment, as Claude Sessions there needs it, and gives
   ///   this process the hub's variables on top, with its stand-in for Claude Code first on the <c>PATH</c>, for the
   ///   sandbox Rider it starts: the hub puts the stand-in on no one's <c>PATH</c>, and the sandbox Rider's goes on to the
   ///   shells of the sessions its Claude Sessions starts. Returns where the hub's parts are.
   /// </summary>
   SandboxHubStarted RunTheHub()
   {
      TakeTheSandboxsOwnEnvironment();
      var hub = _hub.Start();
      foreach(var (name, value) in hub.Environment) Environment.SetEnvironmentVariable(name, value);
      var path = Environment.GetEnvironmentVariable("PATH")!;
      if(!path.StartsWith(hub.FolderOfTheStandInForClaudeCode + Path.PathSeparator))
         Environment.SetEnvironmentVariable("PATH", hub.FolderOfTheStandInForClaudeCode + Path.PathSeparator + path);
      return _hubStarted = hub;
   }

   /// <summary>Where the parts of the sandbox's hub are: it is had run first, unless this process has had it run already.</summary>
   SandboxHubStarted ItsHub => _hubStarted ?? RunTheHub();

   HubsLocalApi LocalApiOfItsHub => new(ItsHub.AnnouncementFolder);

   /// <summary>
   ///   Has the sandbox's hub restart the sandbox Rider, as the published hub restarts Magnus's IDEs for a deploy, its
   ///   restarter running <paramref name="beforeItStartsAgain"/>, unless null, before it starts it again; and returns once
   ///   the hub has started the restart. <see cref="TheIdeRestartOfItsHub"/> tells how it goes, and
   ///   <see cref="IdeRestartsLog"/> has each step.
   /// </summary>
   /// <remarks>
   ///   Through its local API's <c>POST /ide-restart</c>, as Magnus asks himself, so the hub shows him no prompt, whatever
   ///   window is in front. Not as an agent asks, with <c>ClaudeTooling.exe ides restart</c>: that shows Magnus the prompt
   ///   while a window of an IDE being restarted is Windows' foreground window, as the sandbox Rider's may be once it has
   ///   started.
   /// </remarks>
   public void AskItsHubToRestartTheIde(string why, string askedBy, IReadOnlyList<string>? beforeItStartsAgain)
   {
      RunTheHub();
      LocalApiOfItsHub.AskToRestartTheIdes(why, askedBy, [EverydayIde.Rider.NameAtTheHub], beforeItStartsAgain);
   }

   /// <summary>The restart of the IDE the sandbox's hub has under way, or made last: see <see cref="HubsLocalApi.TheIdeRestart"/>.</summary>
   public JsonNode? TheIdeRestartOfItsHub() => LocalApiOfItsHub.TheIdeRestart();

   /// <summary>The log in which the sandbox's hub records each step of each restart of the IDEs it makes, with its time.</summary>
   public string IdeRestartsLog => Path.Combine(ClaudeCodeFolder, "ide-restarts.log");

   /// <summary>
   ///   Stops the sandbox, as <see cref="Stop"/> does, then <see cref="ItsCloyd"/>'s application, and its hub, with the
   ///   sessions that ran in Cloyd: they run in the hub's session hosts, which outlive the sandbox Rider and Cloyd, as
   ///   they do Magnus's IDEs. Returns what the hub's <see cref="SandboxHub.Stop"/> ended.
   /// </summary>
   public string StopAndEndItsSessions()
   {
      Stop();
      ItsCloyd.StopTheApplication();
      _hubStarted = null;
      return _hub.Stop();
   }

   /// <summary>Ends the sessions that ran in the sandbox, in its hub's session hosts, and returns how many it ended.</summary>
   int EndItsSessions() => LocalApiOfItsHub.EndTheSessionHosts();

   /// <summary>
   ///   Closes the sandbox's windows until it exits, as a human closing them would. <see cref="ItsCloyd"/> runs on, and
   ///   the sessions in their session hosts, as Magnus's do when his IDE exits: <see cref="StopAndEndItsSessions"/> ends
   ///   them.
   /// </summary>
   public void Stop()
   {
      var running = RunningProcess();
      if(running is null) return;
      // Only its frames: closing a dialog cancels it. While background tasks run, Rider asks to confirm the exit, and
      // closing that question cancelled the exit, every time it was asked again. Left alone, the question goes, and Rider
      // exits, as soon as the tasks end.
      WaitUntil(() =>
                {
                   if(running.HasExited) return true;
                   foreach(var frame in FramesOf(running).Where(it => !it.IsOwnedByAnotherWindow)) frame.AskToClose();
                   return running.WaitForExit(TimeSpan.FromSeconds(5));
                },
                HowLongStoppingMayTake,
                "the sandbox Rider to exit (its 'Confirm Exit' dialog, if any, waits for background tasks to end)");
      File.Delete(ProcessFile);
   }

   /// <summary>
   ///   Follows the sandbox Rider through a restart it makes itself, as Claude Sessions' <c>POST /restart</c> has it do,
   ///   from the process <paramref name="before"/> it ran as, which has just been asked to restart: waits until that one
   ///   has exited, and the Rider it restarted as has announced Claude Sessions' local API, as it does once a project has
   ///   opened, takes that one as the sandbox's, and returns once the MCP server answers with <paramref name="solution"/>
   ///   loaded and indexed, as <see cref="Start"/> does.
   /// </summary>
   public void FollowItsRestart(int before, string solution)
   {
      // The Rider it restarts as starts once this one has exited, which takes seconds.
      var askedAbout = DateTime.Now.AddSeconds(-1);
      WaitUntil(() => Process.GetProcesses().All(it => it.Id != before), HowLongStoppingMayTake, $"the sandbox Rider, process {before}, to exit as it restarts");
      Process? restarted = null;
      WaitUntil(() => (restarted = TheRiderThatAnnouncedClaudeSessionsLocalApi(startedAfter: askedAbout)) is not null, HowLongStartingMayTake, "the restarted sandbox Rider to announce Claude Sessions' local API");
      var announced = restarted!;
      Record(announced);
      WaitUntil(() => FramesOf(announced).Count > 0, HowLongStartingMayTake, "the restarted sandbox Rider to show a window");
      WaitUntil(() => TheMcpServerHasIndexed(solution), HowLongStartingMayTake, $"the MCP server on port {McpPort} to answer with {solution} loaded and indexed");
      PlaceWindows();
   }

   /// <summary>
   ///   The Rider started after <paramref name="startedAfter"/> whose Claude Sessions announced its local API in the
   ///   sandbox, or null while none has: an announcement a killed Rider left names a process that has exited, or, as
   ///   Windows reuses process ids, another one.
   /// </summary>
   Process? TheRiderThatAnnouncedClaudeSessionsLocalApi(DateTime startedAfter) =>
      !Directory.Exists(LocalApiAnnouncementsFolder)
         ? null
         : Directory.GetFiles(LocalApiAnnouncementsFolder, "*.json")
                    .Select(TheRunningRiderThatWrote)
                    .FirstOrDefault(it => it is not null && it.StartTime > startedAfter);

   /// <summary>
   ///   The running Rider that wrote <paramref name="announcement"/>, which names it by its process id, or null: when
   ///   that process has exited, or is another, which started after the announcement was written, as Windows reuses
   ///   process ids. Such another could be one of Magnus's Riders, which the tools must never take for the sandbox's.
   /// </summary>
   static Process? TheRunningRiderThatWrote(string announcement) =>
      int.TryParse(Path.GetFileNameWithoutExtension(announcement), out var id)
   && Process.GetProcesses().FirstOrDefault(it => it.Id == id) is { HasExited: false } process
   && process.ProcessName.StartsWith("rider", StringComparison.OrdinalIgnoreCase)
   && process.StartTime <= File.GetLastWriteTime(announcement)
         ? process
         : null;

   /// <summary>
   ///   Kills the sandbox Rider, as a crash ends it: nothing of it saves anything as it goes. <see cref="ItsCloyd"/> runs
   ///   on, and the sessions in their session hosts.
   /// </summary>
   public void Kill()
   {
      var running = RunningProcess();
      if(running is null) return;
      running.Kill(entireProcessTree: true);
      running.WaitForExit();
      EndTheLaunchersOfOtherSolutions();
      File.Delete(ProcessFile);
      // Withdrawn as Claude Sessions would have withdrawn it: Windows reuses the process id it names.
      File.Delete(Path.Combine(LocalApiAnnouncementsFolder, $"{running.Id}.json"));
   }

   /// <summary>
   ///   Whether the sandbox runs, where its windows are next to the development IDEA's, and what the MCP server's health
   ///   endpoint answers.
   /// </summary>
   public IReadOnlyList<string> Status()
   {
      if(RunningProcess() is not {} running) return ["The sandbox Rider isn't running."];

      var hub = ItsHub;
      return ScreenThread.Run(() => (IReadOnlyList<string>)
                              [
                                 $"The sandbox Rider runs as process {running.Id}.",
                                 .. FramesOf(running).Select(it => "  sandbox window: " + it.Description),
                                 "  development IDEA window: " + (SandboxWindowPlacement.TheDevelopmentIdeaWindow()?.Description ?? "none open"),
                                 "  MCP health: " + (TheMcpServersHealth() ?? "nothing listens on port " + McpPort),
                                 $"  hub: process {hub.ProcessId}, announced in {hub.Announcement}, its log {hub.Log}",
                                 .. WhatItsCloydShows().Select(it => "  " + it)
                              ]);
   }

   /// <summary>Whether <see cref="ItsCloyd"/>'s application runs, and the project windows it shows.</summary>
   public IReadOnlyList<string> WhatItsCloydShows() =>
      ItsCloyd.TheApplication() is {} application
         ? [$"Cloyd: the application runs as process {application.Id}, from {ItsCloyd}.", .. (ItsCloyd.Windows() ?? []).Select(it => $"  window '{it!["title"]}' of {it["project"]}")]
         : [$"Cloyd: no application runs from {ItsCloyd}."];

   /// <summary>Puts the sandbox's windows, and its Cloyd's, where <see cref="SandboxWindowPlacement"/> says.</summary>
   public void PlaceWindows()
   {
      var running = RunningProcess() ?? throw new InvalidOperationException("The sandbox Rider isn't running.");
      SandboxWindowPlacement.Apply([.. FramesOf(running), .. ItsCloyd.WindowsOnTheDesktop()]);
   }

   /// <summary>
   ///   Where the copy of the fixture solution records <see cref="TheFixtureSolutionsFingerprint"/> as it was copied.
   /// </summary>
   string FixtureCopiedFromFile => Path.Combine(_root, "fixture-solution-copied-from.txt");

   /// <summary>
   ///   The copy replaces the last one, so no run sees what an earlier one changed. That includes a git repository a
   ///   specification made of it and didn't remove, as when it failed: git makes its objects read-only, which Windows
   ///   won't delete as they are.
   /// </summary>
   void CopyTheFixtureSolution()
   {
      CopyTheFixtureSolutionTo(Path.GetDirectoryName(FixtureSolution)!);
      File.WriteAllText(FixtureCopiedFromFile, TheFixtureSolutionsFingerprint());
   }

   /// <summary>
   ///   Makes a fresh copy of the fixture solution in a folder of its own, <paramref name="name"/>, beside the one the
   ///   sandbox opens, for a story that has more than one solution open, and returns its solution file. Stop the sandbox
   ///   before deleting it with <see cref="DeleteTheCopyOfTheFixtureSolution"/>.
   /// </summary>
   public string AnotherCopyOfTheFixtureSolution(string name)
   {
      var copy = Path.Combine(SolutionsFolder, name);
      CopyTheFixtureSolutionTo(copy);
      return Path.Combine(copy, RepositoryRoot.FixtureSolutionName + ".slnx");
   }

   /// <summary>Deletes the copy of the fixture solution <see cref="AnotherCopyOfTheFixtureSolution"/> made as <paramref name="name"/>.</summary>
   public void DeleteTheCopyOfTheFixtureSolution(string name) => DeleteWithItsReadOnlyFiles(Path.Combine(SolutionsFolder, name));

   /// <summary>
   ///   Opens <paramref name="solution"/> in the running sandbox, in a window of its own. The launcher hands it to the
   ///   running Rider, and should exit then; one for a third solution stayed, and held the solutions folder
   ///   (2026-11-19): end it with <see cref="EndTheLaunchersOfOtherSolutions"/> once the solution's window shows, and
   ///   <see cref="Kill"/> ends those still running.
   /// </summary>
   public void OpenAlso(string solution) => _launchersOfOtherSolutions.Add(Launch(solution));

   /// <summary>Ends the launchers <see cref="OpenAlso"/> started that still run, as they have handed their solutions on.</summary>
   public void EndTheLaunchersOfOtherSolutions()
   {
      foreach(var launcher in _launchersOfOtherSolutions.Where(it => !it.HasExited)) launcher.Kill(entireProcessTree: true);
      _launchersOfOtherSolutions.Clear();
   }

   /// <summary>The launchers <see cref="OpenAlso"/> started, which may still run.</summary>
   readonly List<Process> _launchersOfOtherSolutions = [];

   /// <summary>
   ///   Copies the fixture solution to <paramref name="copy"/>, replacing what is there, with its source generator's DLL
   ///   in the copy's <c>SourceGenerators</c> folder, which its Geometry project names as an analyzer.
   /// </summary>
   static void CopyTheFixtureSolutionTo(string copy)
   {
      if(Directory.Exists(copy)) DeleteWithItsReadOnlyFiles(copy);
      CopyFolder(RepositoryRoot.FixtureSolutionFolder, copy);
      var generators = Directory.CreateDirectory(Path.Combine(copy, "SourceGenerators")).FullName;
      File.Copy(TheFixtureSourceGenerator, Path.Combine(generators, Path.GetFileName(TheFixtureSourceGenerator)));
   }

   /// <summary>The fixture solution's source generator, built with these tools, which reference it for this.</summary>
   static string TheFixtureSourceGenerator => Path.Combine(AppContext.BaseDirectory, "RiderTerminalSymbolLinker.FixtureSourceGenerator.dll");

   static void DeleteWithItsReadOnlyFiles(string folder)
   {
      foreach(var file in new DirectoryInfo(folder).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
         file.Attributes &= ~FileAttributes.ReadOnly;
      Directory.Delete(folder, recursive: true);
   }

   static void CopyFolder(string source, string destination)
   {
      Directory.CreateDirectory(destination);
      foreach(var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
      foreach(var folder in Directory.GetDirectories(source).Where(it => Path.GetFileName(it) is not ("bin" or "obj")))
         CopyFolder(folder, Path.Combine(destination, Path.GetFileName(folder)));
   }

   /// <summary>
   ///   Each file of the fixture solution in the repository, with its size and time, apart from what builds put in
   ///   <c>bin</c> and <c>obj</c>, and its source generator's DLL: it changes when the fixture does.
   /// </summary>
   static string TheFixtureSolutionsFingerprint() =>
      string.Join(Environment.NewLine,
                  Directory.GetFiles(RepositoryRoot.FixtureSolutionFolder, "*", SearchOption.AllDirectories)
                           .Where(it => !Path.GetRelativePath(RepositoryRoot.FixtureSolutionFolder, it).Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
                           .Order(StringComparer.OrdinalIgnoreCase)
                           .Append(TheFixtureSourceGenerator)
                           .Select(it => $"{Path.GetRelativePath(RepositoryRoot.FixtureSolutionFolder, it)} {new FileInfo(it).Length} {File.GetLastWriteTimeUtc(it).Ticks}"));

   /// <remarks>
   ///   Through the shell, so Rider gets none of this process's handles. Started directly, it inherited the console
   ///   handles: Rider's output came out in the tool's, and a pipe the tool's output went into stayed open as long as
   ///   Rider ran. The shell can't take environment variables for the one start, so they're set on this process, whose
   ///   environment the started one inherits: see <see cref="TakeTheSandboxsEnvironment"/>.
   /// </remarks>
   Process Launch(string solution)
   {
      TakeTheSandboxsEnvironment();
      var startInformation = new ProcessStartInfo(_rider.Launcher) { UseShellExecute = true };
      startInformation.ArgumentList.Add(solution);
      return Process.Start(startInformation) ?? throw new InvalidOperationException($"Could not start {_rider.Launcher}.");
   }

   /// <summary>
   ///   Gives this process the environment the sandbox Rider starts in, unless it has it: it's started through the shell,
   ///   which can't take environment variables for one start. This process has it once it has had the hub run.
   /// </summary>
   void TakeTheSandboxsEnvironment()
   {
      if(_hubStarted is null) RunTheHub();
   }

   /// <summary>
   ///   Gives this process the environment of the sandbox's processes, its hub's variables apart, which
   ///   <see cref="RunTheHub"/> adds: the hub starts in it too, and serves by the folder of the IDEs' announcements and
   ///   Royder's port.
   /// </summary>
   void TakeTheSandboxsOwnEnvironment()
   {
      LeaveOutTheClaudeCodeSessionThisRunsIn();
      // For the hub's Rider restart: the IDEs it restarts are those announced here, the sandbox Rider's.
      Environment.SetEnvironmentVariable("CLAUDE_SESSIONS_LOCAL_API_FOLDER", LocalApiAnnouncementsFolder);
      // Claude Sessions runs the sandbox's own Cloyd, which serves this folder, apart from Magnus's.
      Environment.SetEnvironmentVariable("CLOYD_FOLDER", ItsCloyd.Folder);
      // The sandbox is in the user's home folder, which is a git repository: without a ceiling, git takes the solutions in
      // the sandbox for part of it.
      Environment.SetEnvironmentVariable("GIT_CEILING_DIRECTORIES", _root);
      Environment.SetEnvironmentVariable("RIDER_PROPERTIES", Path.Combine(_root, "idea.properties"));
      Environment.SetEnvironmentVariable("RIDER_VM_OPTIONS", JvmOptionsFile);
      Environment.SetEnvironmentVariable("DOTNET_ANALYSIS_AND_REFACTORING_MCP_PORT", McpPort.ToString());
      Environment.SetEnvironmentVariable("DOTNET_ANALYSIS_AND_REFACTORING_MCP_TOOLS", _mcpTools.Folder);
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
                            $"dev.magnus.claudesessions.observation.port={ClaudeSessionsObservationPort}",
                            $"dev.magnus.claudesessions.localApi.announcements={Forward(LocalApiAnnouncementsFolder)}"
                         ]);
   }

   /// <summary>
   ///   The sandbox's own JVM options file, which <c>RIDER_VM_OPTIONS</c> names there. Rider's launcher reads it after
   ///   the installation's <c>bin\rider64.exe.vmoptions</c>, so it holds only what overrides that file.
   /// </summary>
   /// <remarks>
   ///   Without it, the launcher reads the everyday Riders' file, <c>%APPDATA%\JetBrains\Rider2026.3\rider64.exe.vmoptions</c>,
   ///   wherever <c>idea.config.path</c> points. The sandbox then ran with Magnus's 16 GB heap, and Toolbox's options.
   /// </remarks>
   string JvmOptionsFile => Path.Combine(_root, "rider64.exe.vmoptions");

   /// <summary>
   ///   Writes the <see cref="JvmOptionsFile"/>, with the installation's default heap of 2 GB, which the fixture solution
   ///   doesn't need more than.
   /// </summary>
   void WriteTheJvmOptionsFile() => File.WriteAllLines(JvmOptionsFile, ["-Xmx2048m"]);

   /// <summary>
   ///   The sandbox's frontend process, which owns its windows, unless it has exited: the one the tools recorded as they
   ///   started it or followed its restart, or else the Rider that announced Claude Sessions' local API in the sandbox,
   ///   which is then recorded.
   /// </summary>
   /// <remarks>
   ///   A restart the tools didn't follow, such as a <c>POST /restart</c> asked by hand, leaves the recorded process
   ///   gone and the sandbox running as another: <c>sandbox stop</c> then said it had stopped the sandbox, and left it
   ///   holding its 4.6 GB (2026-11-24).
   /// </remarks>
   Process? RunningProcess()
   {
      if(TheRecordedProcess() is {} recorded) return recorded;
      if(TheRiderThatAnnouncedClaudeSessionsLocalApi(startedAfter: DateTime.MinValue) is not {} announced) return null;
      Record(announced);
      return announced;
   }

   /// <summary>The process <see cref="ProcessFile"/> names, unless it has exited.</summary>
   Process? TheRecordedProcess()
   {
      if(!File.Exists(ProcessFile)) return null;
      var recorded = File.ReadAllText(ProcessFile).Split(';');
      var process = Process.GetProcesses().FirstOrDefault(it => it.Id == int.Parse(recorded[0]));
      return process is not null && process.StartTime.Ticks == long.Parse(recorded[1]) ? process : null;
   }

   /// <summary>Records <paramref name="process"/> in <see cref="ProcessFile"/> as the sandbox's, by its id and its start, as Windows reuses ids.</summary>
   void Record(Process process) => File.WriteAllText(ProcessFile, $"{process.Id};{process.StartTime.Ticks}");

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
         return McpServerClient.GetStringAsync($"http://127.0.0.1:{McpPort}/health").GetAwaiter().GetResult();
      }
      catch(HttpRequestException exception) when(exception.HttpRequestError == HttpRequestError.ConnectionError)
      {
         return null;
      }
   }

   /// <summary>Whether the MCP server's <c>go_to_definition</c> finds a declaration of <paramref name="qualifiedName"/>.</summary>
   static bool TheMcpServerDeclares(string qualifiedName)
   {
      var request = new JsonObject
      {
         ["jsonrpc"] = "2.0",
         ["id"] = 1,
         ["method"] = "tools/call",
         ["params"] = new JsonObject
         {
            ["name"] = "go_to_definition",
            ["arguments"] = new JsonObject { ["symbolName"] = qualifiedName, ["format"] = "json" }
         }
      };
      using var response = McpServerClient.PostAsync($"http://127.0.0.1:{McpPort}/", new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"))
                                          .GetAwaiter().GetResult();
      var envelope = JsonNode.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())!;
      var answer = JsonNode.Parse(envelope["result"]!["content"]![0]!["text"]!.GetValue<string>())!;
      return answer["declarations"]!.AsArray().Count > 0;
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
