using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using RiderTerminalSymbolLinker.DevelopmentTools.Screen;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Cloyd;

/// <summary>
///   Where an installed Cloyd's project windows are, beside the projects' windows in Magnus's IDEs: the virtual desktop of
///   each, as <c>everyday cloyd</c> tells. Each Cloyd window is to keep to its IDE window's desktop, which Cloyd's
///   specifications, on Avalonia's headless platform, can't check.
/// </summary>
/// <remarks>
///   The IDEs' windows come from their local APIs' <c>GET /windows</c>, which names each one's project and native handle,
///   as announced in <c>%LOCALAPPDATA%\ClaudeSessions\ides</c>. An announcement a crashed IDE left names a process that
///   no longer runs, and a port on which Windows takes 2 s to refuse a connection: it is skipped.
/// </remarks>
static class CloydBesideTheIdes
{
   static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

   static string FolderOfTheIdesAnnouncements => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeSessions", "ides");

   /// <summary>
   ///   A line for each window <paramref name="cloyd"/>'s application shows: its desktop, and its IDE window's. On a
   ///   <see cref="ScreenThread"/>, as the virtual desktops' API wants.
   /// </summary>
   public static IReadOnlyList<string> WhereTheWindowsAre(InstalledCloyd cloyd) => ScreenThread.Run(() => WhereTheWindowsAreOnThisThread(cloyd));

   static IReadOnlyList<string> WhereTheWindowsAreOnThisThread(InstalledCloyd cloyd)
   {
      if(cloyd.Windows() is not {} shown) return [$"No Cloyd application runs from {cloyd}."];
      var onTheDesktop = cloyd.WindowsOnTheDesktop();
      var ideWindows = IdeWindows();
      return [.. shown.Select(it => LineOf(it!["project"]!.GetValue<string>(), it["title"]!.GetValue<string>()))];

      string LineOf(string project, string title)
      {
         var cloydsWindow = onTheDesktop.FirstOrDefault(it => it.Title == title);
         var where = cloydsWindow is null ? "not on the desktop" : $"on {cloydsWindow.DesktopByNumber}";
         if(!ideWindows.TryGetValue(Path.GetFullPath(project), out var idesWindow)) return $"'{title}', {where}: no IDE shows {project}.";
         var together = cloydsWindow is not null && cloydsWindow.DesktopByNumber == idesWindow.DesktopByNumber ? "together" : "APART";
         return $"'{title}', {where}; its IDE's window '{idesWindow.Title}', on {idesWindow.DesktopByNumber}: {together}.";
      }
   }

   /// <summary>The window of each project open in the IDEs that answer, by the project's folder: the first IDE's, by process id, when several show it, as Cloyd takes it.</summary>
   static Dictionary<string, TopLevelWindow> IdeWindows()
   {
      var windows = new Dictionary<string, TopLevelWindow>(StringComparer.OrdinalIgnoreCase);
      if(!Directory.Exists(FolderOfTheIdesAnnouncements)) return windows;
      var running = Process.GetProcesses().Select(it => it.Id).ToHashSet();
      foreach(var port in Directory.EnumerateFiles(FolderOfTheIdesAnnouncements, "*.json")
                                   .Select(it => JsonNode.Parse(File.ReadAllText(it))!)
                                   .Where(it => running.Contains(it["processId"]!.GetValue<int>()))
                                   .OrderBy(it => it["processId"]!.GetValue<int>())
                                   .Select(it => it["port"]!.GetValue<int>()))
         foreach(var window in WindowsListedOn(port).Select(it => it!))
            if(TopLevelWindow.WithHandle(window["window"]!.GetValue<long>()) is {} onTheDesktop)
               windows.TryAdd(Path.GetFullPath(window["project"]!.GetValue<string>()), onTheDesktop);
      return windows;
   }

   static JsonArray WindowsListedOn(int port)
   {
      try
      {
         return JsonNode.Parse(Http.GetStringAsync($"http://127.0.0.1:{port}/windows").GetAwaiter().GetResult())!["windows"]!.AsArray();
      }
      // An announcement a crashed IDE left, whose process id another process got since: nothing answers on its port.
      catch(HttpRequestException notThere) when(notThere.InnerException is SocketException)
      {
         return [];
      }
   }
}
