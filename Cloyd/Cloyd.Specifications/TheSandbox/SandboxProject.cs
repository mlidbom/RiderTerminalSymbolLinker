using System.Text.Json;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Saving;
using Cloyd.SessionHosts;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// A project of a scenario's own, in its <see cref="Sandbox"/>: a folder of its own, whose <see cref="ProjectWindow"/>
/// shows the sandbox hub's session hosts, with the stand-in for Claude Code in them.
/// </summary>
/// <remarks>
/// What it asks the hub it asks on the thread pool and waits for, so that it may be asked on the UI thread too, as a
/// condition of <see cref="SpecificationOpeningWindows.WaitUntil"/> is: there, the hub's answer would wait for the UI
/// thread, which waits for the answer.
/// </remarks>
sealed class SandboxProject
{
   readonly Sandbox _sandbox;
   readonly SavedProjectWindows _savedWindows;

   public SandboxProject(Sandbox sandbox, string name)
   {
      _sandbox = sandbox;
      Project = new Project(Directory.CreateDirectory(Path.Combine(sandbox.ProjectsFolder, name)).FullName);
      _savedWindows = new SavedProjectWindows(new DirectoryInfo(Path.Combine(sandbox.SavedProjectWindowsFolder, name)));
   }

   public Project Project { get; }

   TheHub Hub => _sandbox.HubOfTheSandbox.Hub;

   /// <summary>
   /// A new window of the project, not yet shown, one of the sandbox's <see cref="Sandbox.WindowsOfCloyd"/> once it opens,
   /// which saves itself where the project's next window finds it, finds the project's window in the sandbox's IDEs, and
   /// its C# symbols in the solutions the sandbox's Rider has loaded, and saves the images pasted into it in the sandbox's
   /// <see cref="Sandbox.CloydsFolder"/>.
   /// </summary>
   public ProjectWindow Window() => Window(_sandbox.WindowsOfCloyd);

   /// <summary>A new window of the project, as <see cref="Window()"/>, one of <paramref name="windowsOfCloyd"/> once it opens, as a Cloyd application's.</summary>
   public ProjectWindow Window(ProjectWindows windowsOfCloyd) =>
      new(Project, windowsOfCloyd, Hub, _sandbox.Ides, _sandbox.DotNetAnalysisAndRefactoringMcp.Mcp, _sandbox.ShellOfItsSessions, _savedWindows, _sandbox.CloydsFolder.PastedImages);

   /// <summary>What the project's windows saved last.</summary>
   public SavedProjectWindow SavedWindow => _savedWindows.Of(Project);

   /// <summary>Ends the session host <paramref name="id"/>, as a reboot ends them all.</summary>
   public void End(SessionHostId id) => Answer(() => Hub.End(id));

   /// <summary>A session host started in the project's folder, labelled <paramref name="label"/>, as another window or an IDE's tab would start it.</summary>
   public SessionHostOfTheHub StartASessionHost(string label) => Answer(() => Hub.StartASessionHost(_sandbox.ShellOfItsSessions, Project.Folder, label));

   /// <summary>The hub's session hosts whose shells started in the project's folder.</summary>
   public IReadOnlyList<SessionHostOfTheHub> ItsSessionHosts() =>
      Answer(() => Hub.SessionHosts()).Where(it => new Project(it.Directory).Equals(Project)).ToList();

   /// <summary>The hub's session host <paramref name="id"/>: null once it has ended.</summary>
   public SessionHostOfTheHub? SessionHost(SessionHostId id) => Answer(() => Hub.SessionHosts()).SingleOrDefault(it => it.Id == id);

   /// <summary>The Claude Code that runs in the host <paramref name="id"/>, as the hub lists the running sessions now: null while none does.</summary>
   public RunningSession? SessionRunningIn(SessionHostId id) => Answer(async () =>
   {
      using var cancellation = new CancellationTokenSource();
      await foreach(var runningSessions in Hub.RunningSessionsAsTheyChange(cancellation.Token))
         return runningSessions.SingleOrDefault(it => it.SessionHost == id);
      return null;
   });

   /// <summary>The label of a host that Claude Sessions' tab in an IDE started for the project.</summary>
   public string LabelOfAClaudeSessionsTab => $$"""{"ideConfigFolder":"C:\\Users\\Someone\\AppData\\Roaming\\JetBrains\\Rider2026.3","project":{{JsonSerializer.Serialize(Project.Folder)}}}""";

   /// <summary>The label Cloyd's windows start the project's hosts with.</summary>
   public string LabelOfCloydsHosts => SessionHostLabel.OfANewHostOf(Project);

   /// <summary>What <paramref name="asked"/> answers, asked on the thread pool, where no UI thread's context holds its continuations.</summary>
   static TAnswer Answer<TAnswer>(Func<Task<TAnswer>> asked) => Task.Run(asked).GetAwaiter().GetResult();

   static void Answer(Func<Task> asked) => Task.Run(asked).GetAwaiter().GetResult();
}
