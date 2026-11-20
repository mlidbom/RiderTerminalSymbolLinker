using System.Diagnostics;
using Cloyd.Ides;
using Cloyd.LocalApi;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The machine as the specifications see it: a <see cref="SandboxHub"/> of their own, whose sessions run the tooling
/// owner's stand-in for Claude Code. So a specification starts real session hosts, with a Claude Code in each, in
/// seconds and at no cost, and never touches a session of Magnus's. Started once, as the first specification needs it.
/// </summary>
/// <remarks>
/// The stand-in (<c>~/.claude/src/StandInForClaudeCode</c>) runs a session as Claude Code does: it keeps its record and
/// transcript where <c>CLAUDE_CONFIG_DIR</c> says, which the hub reads, and draws an input box as Claude Code does. It
/// says <c>New session &lt;id&gt;</c> as it starts, and the size of its terminal.<br/>
/// The specifications' process takes the hub's <see cref="SandboxHub.Environment"/>, and so does every process it starts,
/// such as a Cloyd application: what it asks of a hub it asks the sandbox's.
/// </remarks>
sealed class Sandbox
{
   static readonly Lazy<Sandbox> StartedOnce = new(() => new Sandbox());

   readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloydSpecifications");

   Sandbox()
   {
      HubOfTheSandbox = SandboxHub.RunIn(Path.Combine(_root, "hub"));
      foreach(var (variable, value) in HubOfTheSandbox.Environment) Environment.SetEnvironmentVariable(variable, value);
      //todo: The tooling owner, 2026-11-20: the hub is to put the stand-in first on its sandbox sessions' PATH itself.
      //   Once it does, the sessions' shell needs no PATH of its own.
      ShellOfItsSessions = (ShellOfNewSessions.OnThisMachine() with { CommandLine = "pwsh.exe -NoLogo -NoProfile" })
        .With("PATH", HubOfTheSandbox.FolderOfTheStandInForClaudeCode + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
   }

   public static Sandbox Started => StartedOnce.Value;

   public SandboxHub HubOfTheSandbox { get; }

   /// <summary>
   /// The shell the sessions of the specifications' project windows start in: PowerShell 7 without Magnus's profile, so
   /// that it starts the same anywhere, with the stand-in for Claude Code first on its <c>PATH</c>.
   /// </summary>
   public ShellOfNewSessions ShellOfItsSessions { get; }

   /// <summary>The folder of the specifications' projects, each in a folder of its own.</summary>
   public string ProjectsFolder => Path.Combine(_root, "projects");

   /// <summary>
   /// The IDEs as the specifications' project windows see them: the <see cref="StandInIde"/>s announced in a folder of the
   /// sandbox's own, so that no window of a specification asks Magnus's IDEs.
   /// </summary>
   public TheIdes Ides => new(new DirectoryInfo(FolderOfTheIdes));

   /// <summary>Rider's .NET Analysis&amp;Refactoring MCP, as the specifications' project windows see it, with the solutions they load.</summary>
   public StandInDotNetAnalysisAndRefactoringMcp DotNetAnalysisAndRefactoringMcp { get; } = new();

   /// <summary>
   /// The windows of Cloyd that the specifications open themselves, each through its <see cref="SandboxProject"/>, one
   /// after another: so a window's history picker finds the tabs of the specification's other windows. It opens no window
   /// itself, as a Cloyd application does when handed a project.
   /// </summary>
   public ProjectWindows WindowsOfCloyd { get; } = new((project, _) => throw new InvalidOperationException($"The specifications open their windows themselves, through their SandboxProject, not {project}'s through the sandbox's WindowsOfCloyd."));

   /// <summary>The folder in which the <see cref="StandInIde"/>s announce their local APIs.</summary>
   public string FolderOfTheIdes => Path.Combine(_root, "ides");

   /// <summary>The folder where the specifications' projects save their windows, each in a folder of its own.</summary>
   public string SavedProjectWindowsFolder => Path.Combine(_root, "project-windows");

   /// <summary>
   /// A <see cref="CloydsFolder"/> of the specification <paramref name="name"/>'s own, with nothing in it: the Cloyd
   /// application that serves it, left by an earlier run that stopped before it ended it, is ended first.
   /// </summary>
   public CloydsFolder EmptyCloydsFolder(string name)
   {
      var folder = new CloydsFolder(new DirectoryInfo(Path.Combine(_root, "cloyd", name)));
      if(new TheCloydApplication(folder).ProcessId is {} left && left != Environment.ProcessId)
         using(var application = Process.GetProcessById(left))
         {
            application.Kill();
            application.WaitForExit();
         }

      if(folder.Folder.Exists) folder.Folder.Delete(recursive: true);
      return folder;
   }
}
