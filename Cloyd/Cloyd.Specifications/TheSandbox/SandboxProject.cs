using System.Text.Json;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.SessionHosts;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// A project of a specification's own, in the <see cref="Sandbox"/>: a folder of its own, whose
/// <see cref="ProjectWindow"/> shows the sandbox hub's session hosts, with the stand-in for Claude Code in them. Its
/// session hosts are ended when the specification is done with it.
/// </summary>
/// <remarks>
/// What it asks the hub it asks on the thread pool and waits for, so that it may be asked on the UI thread too, as a
/// condition of <see cref="SpecificationOpeningWindows.WaitUntil"/> is: there, the hub's answer would wait for the UI
/// thread, which waits for the answer.
/// </remarks>
sealed class SandboxProject : IDisposable
{
   SandboxProject(string name)
   {
      var folder = Directory.CreateDirectory(Path.Combine(Sandbox.Started.ProjectsFolder, name));
      Project = new Project(folder.FullName);
      EndItsSessionHosts(); // Left by an earlier run that stopped before it ended them.
   }

   /// <summary>The project of the folder <paramref name="name"/>, such as the name of the specification.</summary>
   public static SandboxProject Named(string name) => new(name);

   public Project Project { get; }

   TheHub Hub => Sandbox.Started.HubOfTheSandbox.Hub;

   /// <summary>A new window of the project, not yet shown.</summary>
   public ProjectWindow Window() => new(Project, Hub, Sandbox.Started.ShellOfItsSessions);

   /// <summary>A session host started in the project's folder, labelled <paramref name="label"/>, as another window or an IDE's tab would start it.</summary>
   public SessionHostOfTheHub StartASessionHost(string label) => Answer(() => Hub.StartASessionHost(Sandbox.Started.ShellOfItsSessions, Project.Folder, label));

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

   public void Dispose() => EndItsSessionHosts();

   void EndItsSessionHosts()
   {
      foreach(var host in ItsSessionHosts()) Answer(() => Hub.End(host.Id));
   }

   /// <summary>What <paramref name="asked"/> answers, asked on the thread pool, where no UI thread's context holds its continuations.</summary>
   static TAnswer Answer<TAnswer>(Func<Task<TAnswer>> asked) => Task.Run(asked).GetAwaiter().GetResult();

   static void Answer(Func<Task> asked) => Task.Run(asked).GetAwaiter().GetResult();
}
