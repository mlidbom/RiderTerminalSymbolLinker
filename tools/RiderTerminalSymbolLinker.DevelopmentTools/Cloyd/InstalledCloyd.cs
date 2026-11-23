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
   internal string Bin => Path.Combine(Folder, "bin");

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
   public JsonArray? Windows() => WindowInventory()?["windows"]!.AsArray();

   JsonNode? WindowInventory()
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
         return JsonNode.Parse(answer.Content.ReadAsStringAsync().GetAwaiter().GetResult());
      }
   }

   /// <summary>The folders of the projects whose windows the Cloyd application shows: none when no application runs.</summary>
   public IReadOnlyList<string> ProjectsItShows() => ProjectsIn(Windows());

   static string[] ProjectsIn(JsonArray? windows) => [.. (windows ?? []).Select(it => Path.GetFullPath(it!["project"]!.GetValue<string>()))];

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
   ///   What the application's announcement says, its process, port and token: null while there is none. Read sharing
   ///   delete access, as the application moves it into place: a read that doesn't share it fails at that moment, and
   ///   makes the move fail.
   /// </summary>
   JsonNode? TheAnnouncement()
   {
      try
      {
         using var announcement = new FileStream(Path.Combine(Folder, "local-api.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
         return JsonNode.Parse(announcement);
      }
      catch(Exception failure) when(failure is FileNotFoundException or DirectoryNotFoundException)
      {
         return null;
      }
   }

   public override string ToString() => Bin;
}
