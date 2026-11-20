using System.Net.Http.Headers;
using Cloyd.LocalApi;
using Cloyd.LocalApi.Contract;
using Cloyd.Projects;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// A specification that runs the Cloyd application in the specifications' own process: its <see cref="ProjectWindows"/>
/// on Avalonia's headless platform, and its <see cref="CloydsLocalApi"/>, announced in a <see cref="CloydsFolder"/> of the
/// specification's own. A project is handed to it as <c>Cloyd.exe --project</c> hands one, through the
/// <see cref="TheCloydApplication"/> of the folder. Its windows are closed, and its API stopped, when the specification is
/// done.
/// </summary>
public abstract class SpecificationRunningTheCloydApplication : SpecificationOpeningWindows
{
   static readonly HttpClient Http = new();

   readonly CloydsLocalApi _localApi;

   /// <summary>Runs the application, with a <see cref="CloydsFolder"/> named <paramref name="name"/>.</summary>
   private protected SpecificationRunningTheCloydApplication(string name)
   {
      CloydsFolder = Sandbox.Started.EmptyCloydsFolder(name);
      Windows = OnTheUIThread(() => new ProjectWindows(project => Projects.Single(it => it.Project.Equals(project)).Window()));
      _localApi = CloydsLocalApi.Start(Windows, CloydsFolder);
      Application = new TheCloydApplication(CloydsFolder);
   }

   /// <summary>The projects handed to the application, whose windows it shows: ended with the specification.</summary>
   private protected abstract IReadOnlyList<SandboxProject> Projects { get; }

   private protected CloydsFolder CloydsFolder { get; }

   private protected ProjectWindows Windows { get; }

   /// <summary>The application as a client reaches it, as <c>Cloyd.exe --project</c> does.</summary>
   TheCloydApplication Application { get; }

   /// <summary>Hands <paramref name="project"/> to the application, and returns the window it shows once it has brought back the project's tabs.</summary>
   private protected WindowInTheAnswer HandedOver(SandboxProject project)
   {
      var shown = Answered(() => Application.ShowTheWindowOf(project.Project), $"the application showed the window of {project.Project.Name}");
      var window = OnTheUIThread(() => Windows.Open.Single(it => it.Project.Equals(project.Project)));
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      return shown;
   }

   /// <summary>The windows the application lists, through its local API.</summary>
   private protected IReadOnlyList<WindowInTheAnswer> WindowsItLists() => Answered(() => Application.Windows()!, "the application listed its windows");

   /// <summary>
   /// What the application's API answers <c>POST /<paramref name="pathAndQuery"/></c> with an empty body, asked with the
   /// token of its announcement, as a client that read it asks, such as the hub.
   /// </summary>
   private protected HttpResponseMessage Posted(string pathAndQuery)
   {
      var announcement = AnnouncementOfCloydsLocalApi.In(CloydsFolder)!;
      return Answered(() =>
      {
         using var request = new HttpRequestMessage(HttpMethod.Post, announcement.AddressOf(pathAndQuery)) { Content = new ByteArrayContent([]) };
         request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", announcement.Token);
         return Http.Send(request);
      }, $"the application answered POST /{pathAndQuery}");
   }

   /// <summary>
   /// What <paramref name="asked"/> answers, asked on the thread pool, while the UI thread runs what the application's API
   /// has it do: asked on this thread, the API would wait for the UI thread, which would wait for this one.
   /// </summary>
   private protected TAnswer Answered<TAnswer>(Func<TAnswer> asked, string what)
   {
      var asking = Task.Run(asked);
      WaitUntil(() => asking.IsCompleted, what);
      return asking.GetAwaiter().GetResult();
   }

   public override void Dispose()
   {
      _localApi.Stop();
      foreach(var window in OnTheUIThread(() => Windows.Open.ToList())) Close(window);
      base.Dispose();
      foreach(var project in Projects) project.Dispose();
   }
}
