using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;
using RiderTerminalSymbolLinker.DevelopmentTools.Screen;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Cloyd;

/// <summary>
///   A Cloyd installed in Cloyd's folder, <paramref name="folder"/>, as Claude Sessions finds it: its build in
///   <c>bin</c>, whose <c>Cloyd.exe --project</c> Claude Sessions runs as a project opens, and the Cloyd application that
///   command starts, which announces its local API in the folder. Magnus's is <see cref="Everyday"/>; the
///   <see cref="Riders.SandboxRider"/> has one of its own, which <c>CLOYD_FOLDER</c> names there.
/// </summary>
/// <remarks>
///   The application runs from <c>bin</c>, and Windows refuses to overwrite a file a running program maps, but lets it be
///   moved: so a new build goes in through a <see cref="FolderMirror"/>, and the application that runs runs on with the
///   build it has, until its last window closes.
/// </remarks>
public sealed class InstalledCloyd(string folder)
{
   static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
   static readonly TimeSpan HowLongItsApplicationMayTakeToExit = TimeSpan.FromSeconds(10);

   /// <summary>Magnus's Cloyd, in <c>%LOCALAPPDATA%\Cloyd</c>, which his IDEs' Claude Sessions run.</summary>
   internal static InstalledCloyd Everyday { get; } = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cloyd"));

   /// <summary>Cloyd's folder, as <c>CLOYD_FOLDER</c> names it for Claude Sessions and for Cloyd itself.</summary>
   internal string Folder { get; } = folder;

   /// <summary>Where the build is: <c>Cloyd.exe</c> and what it runs on.</summary>
   string Bin => Path.Combine(Folder, "bin");

   string CloydExe => Path.Combine(Bin, "Cloyd.exe");

   /// <summary>Whether a build of Cloyd is in <see cref="Bin"/>.</summary>
   internal bool IsInstalled => File.Exists(CloydExe);

   /// <summary>Whether <see cref="Bin"/> holds exactly <paramref name="build"/>.</summary>
   internal bool Has(string build) => FolderMirror.HoldTheSame(build, Bin);

   /// <summary>
   ///   Puts <paramref name="build"/> in <see cref="Bin"/>, in place of the build there, and returns whether it changed
   ///   anything. An application that runs runs on with the build it has.
   /// </summary>
   internal bool Install(string build) => new FolderMirror(Path.Combine(Folder, "replaced-files")).Mirror(build, Bin);

   /// <summary>
   ///   Puts <paramref name="build"/> in, as a deploy does, unless <paramref name="dryRun"/>, and returns what that does,
   ///   or would do: when the next Cloyd application starts it.
   /// </summary>
   internal string Deploy(string build, bool dryRun)
   {
      if(Has(build)) return $"Cloyd in {this} has this build already.";
      var when = !IsInstalled ? "where it wasn't yet: the next project to open starts it"
               : TheApplication() is {} running ? $"and the Cloyd application that runs, process {running.Id}, runs the build it has until its last window closes"
               : "and the next project to open starts it";
      if(dryRun) return $"Cloyd's build would go into {this}, {when}.";
      Install(build);
      return $"Put Cloyd's build into {this}, {when}.";
   }

   /// <summary>The Cloyd application that runs, as its announcement names it: null when none does.</summary>
   public Process? TheApplication()
   {
      if(TheAnnouncement() is not {} announcement) return null;
      var processId = announcement["processId"]!.GetValue<int>();
      // An announcement a crash left may name a process id Windows has given another process since.
      return Process.GetProcesses().FirstOrDefault(it => it.Id == processId && it.ProcessName == "Cloyd");
   }

   /// <summary>
   ///   The windows the Cloyd application shows, as its <c>GET /windows</c> lists them, each with its project's folder and
   ///   its title: null when no application runs.
   /// </summary>
   public JsonArray? Windows()
   {
      if(TheApplication() is null || TheAnnouncement() is not {} announcement) return null;
      using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{announcement["port"]!.GetValue<int>()}/windows");
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", announcement["token"]!.GetValue<string>());
      HttpResponseMessage answer;
      try
      {
         answer = Http.Send(request);
      }
      catch(HttpRequestException failure) when(failure.HttpRequestError == HttpRequestError.ConnectionError)
      {
         return null; // The application stops, as its last window has closed: its announcement goes last.
      }

      using(answer)
      {
         answer.EnsureSuccessStatusCode();
         return JsonNode.Parse(answer.Content.ReadAsStringAsync().GetAwaiter().GetResult())!["windows"]!.AsArray();
      }
   }

   /// <summary>The folders of the projects whose windows the Cloyd application shows: none when no application runs.</summary>
   public IReadOnlyList<string> ProjectsItShows() => [.. (Windows() ?? []).Select(it => Path.GetFullPath(it!["project"]!.GetValue<string>()))];

   /// <summary>The Cloyd application's windows on the desktop: none when no application runs.</summary>
   internal IReadOnlyList<TopLevelWindow> WindowsOnTheDesktop() =>
      TheApplication() is {} application ? [.. TopLevelWindow.AllVisible().Where(it => it.ProcessId == application.Id && it.Title.Length > 0)] : [];

   /// <summary>
   ///   Ends the Cloyd application, unless none runs, as closing its last window does: each window saves its tabs, and
   ///   lets go of their sessions, which run on. One that doesn't exit within seconds is killed.
   /// </summary>
   public void StopTheApplication()
   {
      if(TheApplication() is not {} application) return;
      foreach(var window in WindowsOnTheDesktop()) window.AskToClose();
      if(!application.WaitForExit(HowLongItsApplicationMayTakeToExit))
      {
         application.Kill();
         application.WaitForExit();
      }
   }

   /// <summary>
   ///   Has the Cloyd application take the build in <see cref="Bin"/>, as a reload has an IDE take a plugin's: stops it,
   ///   its windows saving their tabs, and runs <c>Cloyd.exe --project</c> for each project it showed, as Claude Sessions
   ///   does as a project opens, which starts it again. The sessions run on in their hosts, and each window brings its tabs
   ///   back. Returns what it did.
   /// </summary>
   internal string RestartTheApplication()
   {
      if(TheApplication() is not {} before) return $"No Cloyd application runs: the next project to open starts one with the build in {this}.";
      var projects = ProjectsItShows();
      StopTheApplication();
      foreach(var project in projects) ShowTheWindowOf(project);
      return $"Restarted the Cloyd application on the build in {this}: process {before.Id} is now {TheApplication()?.Id.ToString() ?? "none"}, showing {string.Join(", ", projects)}.";
   }

   /// <summary>Runs <c>Cloyd.exe --project <paramref name="project"/></c>, which hands the project to the application, starting it if none runs, and returns once its window shows.</summary>
   void ShowTheWindowOf(string project)
   {
      using var command = Process.Start(new ProcessStartInfo(CloydExe, ["--project", project]) { UseShellExecute = false })!;
      command.WaitForExit();
      if(command.ExitCode != 0) throw new InvalidOperationException($"{CloydExe} --project {project} exited with {command.ExitCode}.");
   }

   /// <summary>What the application's announcement says, its process, port and token: null while there is none.</summary>
   JsonNode? TheAnnouncement()
   {
      try
      {
         return JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, "local-api.json")));
      }
      catch(Exception failure) when(failure is FileNotFoundException or DirectoryNotFoundException)
      {
         return null;
      }
   }

   public override string ToString() => Bin;
}
