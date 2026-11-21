using System.Diagnostics;
using ClaudeTooling.Hub.LocalApi.Contract;
using Compze.Threading;
using Compze.Threading.Interprocess;
using Cloyd.Ides;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The machine as a scenario of the specifications sees it: a <see cref="SandboxHub"/> of its own, whose sessions run the
/// tooling owner's stand-in for Claude Code, and projects, IDEs, saved windows and windows of Cloyd of its own. So a
/// scenario starts real session hosts, with a Claude Code in each, in seconds and at no cost, sees nothing that another
/// scenario did, and never touches a session of Magnus's. A scenario starts its sandbox as it first asks for it, through
/// <see cref="SpecificationOpeningWindows.Sandbox"/>, and the sandbox ends with the scenario: its hub stops, and its folder
/// is deleted. Both happen off the scenarios' path: <see cref="SandboxesOfThisRun"/> starts each ahead, and ends it in the
/// background.
/// </summary>
/// <remarks>
/// The stand-in (<c>~/.claude/src/StandInForClaudeCode</c>) runs a session as Claude Code does: it keeps its record and
/// transcript where <c>CLAUDE_CONFIG_DIR</c> says, which the hub reads, tells the hub of its turns as the
/// <c>tooling-commands</c> mod tells it of Claude Code's, and draws an input box as Claude Code does. It
/// says <c>New session &lt;id&gt;</c> as it starts, and the size of its terminal.<br/>
/// A process that a scenario starts, such as a <c>Cloyd.exe</c>, takes the hub's <see cref="SandboxHub.Environment"/>, so
/// that what it asks of a hub it asks the scenario's. The specifications' own process never takes it: it runs scenarios
/// one after another, each with a hub of its own.
/// </remarks>
/// <remarks>
/// Each sandbox is a folder of its own under <c>%LOCALAPPDATA%\CloydSpecifications</c>, named by the process of its run
/// and its number in the run, such as <c>41236-7</c>: short, as the hub's Claude Code folder names a folder by the whole
/// path of each project's folder. A sandbox's hub stops itself once its run's process has exited, however it ended
/// (<see cref="SandboxHub.RunIn"/>). As a run's first sandbox starts, the sandboxes of runs that stopped before their
/// scenarios ended are stopped and deleted: those named by a process that no longer runs. One run at a time does it,
/// under a lock of the machine's, so that runs started together don't end the same sandboxes at once.
/// </remarks>
sealed class Sandbox : IDisposable
{
   static readonly DirectoryInfo FolderOfTheSandboxes = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloydSpecifications"));

   static int _startedInThisRun;

   readonly DirectoryInfo _folder;
   readonly Lazy<StandInDotNetAnalysisAndRefactoringMcp> _dotNetAnalysisAndRefactoringMcp = new(() => new StandInDotNetAnalysisAndRefactoringMcp());

   /// <summary>Ends the sandboxes of the runs that stopped before their scenarios ended, before this run's first starts.</summary>
   static Sandbox()
   {
      using var endingThem = IMutex.Local("Cloyd.Specifications.EndingTheSandboxesOfRunsThatEnded", LockTimeout.Seconds(120));
      endingThem.Locked(EndThoseOfRunsThatEnded);
   }

   Sandbox(DirectoryInfo folder)
   {
      _folder = folder;
      HubOfTheSandbox = HubStartedIn(folder);
      //todo: The tooling owner, 2026-11-20: the hub is to put the stand-in first on its sandbox sessions' PATH itself.
      //   Once it does, the sessions' shell needs no PATH of its own.
      //todo: Asked of the tooling owner, 2026-11-21: the hub is to name its announcement's folder to its sandbox sessions
      //   itself, as it names its Claude Code folder, so that the stand-in tells it of its turns. Once it does, the
      //   sessions' shell needs no CLAUDE_TOOLING_LOCAL_API_FOLDER of its own.
      ShellOfItsSessions = (ShellOfNewSessions.OnThisMachine() with { CommandLine = "pwsh.exe -NoLogo -NoProfile" })
        .With("PATH", HubOfTheSandbox.FolderOfTheStandInForClaudeCode + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"))
        .With(WhatTheAnnouncementSays.VariableNamingTheFolder, HubOfTheSandbox.Environment[WhatTheAnnouncementSays.VariableNamingTheFolder]);
      WindowsOfCloyd = new ProjectWindows((project, _) => throw new InvalidOperationException($"The specifications open their windows themselves, through their SandboxProject, not {project}'s through the sandbox's WindowsOfCloyd."));
   }

   /// <summary>A fresh sandbox, once its hub runs: the one <see cref="SandboxesOfThisRun"/> started ahead.</summary>
   public static Sandbox Started() => SandboxesOfThisRun.Next();

   /// <summary>A new sandbox, once its hub runs: started now, as <see cref="SandboxesOfThisRun"/> starts each ahead.</summary>
   public static Sandbox StartedNow() => new(new DirectoryInfo(Path.Combine(FolderOfTheSandboxes.FullName, $"{Environment.ProcessId}-{Interlocked.Increment(ref _startedInThisRun)}")));

   public SandboxHub HubOfTheSandbox { get; }

   /// <summary>
   /// The shell the sessions of the sandbox's project windows start in: PowerShell 7 without Magnus's profile, so that it
   /// starts the same anywhere, with the hub's stand-in for Claude Code first on its <c>PATH</c>, and the folder of the
   /// hub's announcement in <c>CLAUDE_TOOLING_LOCAL_API_FOLDER</c>, so that the stand-in tells the hub of its turns.
   /// </summary>
   public ShellOfNewSessions ShellOfItsSessions { get; }

   /// <summary>The folder of the sandbox's projects, each in a folder of its own.</summary>
   public string ProjectsFolder => Path.Combine(_folder.FullName, "projects");

   /// <summary>
   /// The IDEs as the sandbox's project windows see them: the <see cref="StandInIde"/>s announced in the sandbox's
   /// <see cref="FolderOfTheIdes"/>, so that no window of a specification asks Magnus's IDEs.
   /// </summary>
   public TheIdes Ides => new(new DirectoryInfo(FolderOfTheIdes));

   /// <summary>The folder in which the <see cref="StandInIde"/>s announce their local APIs.</summary>
   public string FolderOfTheIdes => Path.Combine(_folder.FullName, "ides");

   /// <summary>Rider's .NET Analysis&amp;Refactoring MCP, as the sandbox's project windows see it, with the solutions it has loaded.</summary>
   public StandInDotNetAnalysisAndRefactoringMcp DotNetAnalysisAndRefactoringMcp => _dotNetAnalysisAndRefactoringMcp.Value;

   /// <summary>
   /// The windows of Cloyd that the scenario opens itself, each through its <see cref="SandboxProject"/>: so a window's
   /// history picker finds the tabs of the scenario's other windows. It opens no window itself, as a Cloyd application
   /// does when handed a project.
   /// </summary>
   public ProjectWindows WindowsOfCloyd { get; }

   /// <summary>The folder where the sandbox's projects save their windows.</summary>
   public string SavedProjectWindowsFolder => Path.Combine(_folder.FullName, "project-windows");

   /// <summary>The <see cref="Cloyd.CloydsFolder"/> of the Cloyd application that serves the sandbox's projects, as the scenario runs one.</summary>
   public CloydsFolder CloydsFolder => new(new DirectoryInfo(Path.Combine(_folder.FullName, "cloyd")));

   /// <summary>The project of the folder <paramref name="name"/> in the sandbox's <see cref="ProjectsFolder"/>, with no window saved.</summary>
   public SandboxProject Project(string name) => new(this, name);

   /// <summary>
   /// Stops the stand-in for Rider's MCP, then has the sandbox <see cref="End"/> in the background, as its scenario has
   /// ended: <see cref="SandboxesOfThisRun"/>.
   /// </summary>
   public void Dispose()
   {
      if(_dotNetAnalysisAndRefactoringMcp.IsValueCreated) _dotNetAnalysisAndRefactoringMcp.Value.Dispose();
      SandboxesOfThisRun.EndInTheBackground(this);
   }

   /// <summary>Stops the sandbox's hub, with every session host it runs, then deletes the sandbox's folder.</summary>
   public void End() => EndTheSandboxIn(_folder);

   static string FolderOfTheHub(DirectoryInfo sandbox) => Path.Combine(sandbox.FullName, "hub");

   /// <summary>
   /// The hub of the sandbox <paramref name="folder"/>, once it runs. A start that fails ends what it started of the
   /// sandbox, which no scenario has to end, and fails.
   /// </summary>
   static SandboxHub HubStartedIn(DirectoryInfo folder)
   {
      try
      {
         return SandboxHub.RunIn(FolderOfTheHub(folder));
      }
      catch
      {
         EndTheSandboxIn(folder);
         throw;
      }
   }

   /// <summary>Stops and deletes the sandboxes named by a process that no longer runs: those of runs that stopped before their scenarios ended.</summary>
   static void EndThoseOfRunsThatEnded()
   {
      if(!FolderOfTheSandboxes.Exists) return;

      var running = Process.GetProcesses().Select(it =>
      {
         using(it) return it.Id;
      }).ToHashSet();
      foreach(var left in FolderOfTheSandboxes.EnumerateDirectories().Where(it => RunOf(it) is {} run && !running.Contains(run))) EndTheSandboxIn(left);
   }

   /// <summary>The process of the run whose sandbox <paramref name="folder"/> is: null for a folder that is no sandbox.</summary>
   static int? RunOf(DirectoryInfo folder) => folder.Name.Split('-') is [var run, var number] && int.TryParse(run, out var process) && int.TryParse(number, out _) ? process : null;

   /// <summary>
   /// Stops the hub of the sandbox <paramref name="folder"/>, if it got as far as starting one, then deletes the folder,
   /// whose git repositories' object files git writes read-only.
   /// </summary>
   static void EndTheSandboxIn(DirectoryInfo folder)
   {
      if(Directory.Exists(FolderOfTheHub(folder))) SandboxHub.StopIn(FolderOfTheHub(folder));
      foreach(var file in folder.EnumerateFiles("*", SearchOption.AllDirectories)) file.Attributes = FileAttributes.Normal;
      folder.Delete(recursive: true);
   }
}
