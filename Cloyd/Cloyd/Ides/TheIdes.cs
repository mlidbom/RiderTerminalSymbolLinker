using System.Net.Sockets;
using System.Text.Json;
using Cloyd.Projects;

namespace Cloyd.Ides;

/// <summary>
/// The JetBrains IDEs on the machine that run Claude Sessions, the plugin that has Cloyd show each project they open, as
/// the files in a folder that announce their local APIs tell of them (<see cref="AnnouncementOfAnIdesLocalApi"/>).
/// Asked, each lists the windows of its open projects, with the virtual desktop each is on: so Cloyd finds a project's
/// <see cref="IdeWindow"/>, whose desktop the project's <see cref="ProjectWindow"/> keeps to.
/// </summary>
/// <remarks>
/// <c>GET /windows</c> needs no token, as it changes nothing.<br/>
/// A file a crashed IDE left names a process that no longer runs, and a port on which Windows takes 2 s to refuse a
/// connection: so an IDE whose process doesn't run isn't asked. One that doesn't answer within
/// <see cref="TimeAnIdeHasToAnswer"/> lists no window: it answers in milliseconds.
/// </remarks>
sealed class TheIdes(DirectoryInfo folderOfTheirAnnouncements)
{
   static readonly TimeSpan TimeAnIdeHasToAnswer = TimeSpan.FromSeconds(1);
   static readonly HttpClient Http = new() { Timeout = TimeAnIdeHasToAnswer };

   readonly string _folderOfTheirAnnouncements = folderOfTheirAnnouncements.FullName;

   /// <summary>
   /// Magnus's IDEs, which announce their APIs in <c>%LOCALAPPDATA%\ClaudeSessions\ides</c>. The sandbox IDE of Claude
   /// Sessions' development announces in a folder of its own.
   /// </summary>
   public static TheIdes OnThisMachine() => new(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeSessions", "ides")));

   /// <summary>
   /// The window of <paramref name="project"/> in the IDEs that answer: when several show it, the one of the IDE whose
   /// process id is lowest, so that the same one is found each time. Null when none shows it.
   /// </summary>
   public async Task<IdeWindow?> WindowOf(Project project, CancellationToken cancellation)
   {
      var listed = await Task.WhenAll(Announcements().Where(it => it.ItsIdeRuns).OrderBy(it => it.ProcessId).Select(it => WindowsListedBy(it, cancellation)));
      return listed.SelectMany(it => it).FirstOrDefault(it => it.Project.Equals(project));
   }

   IEnumerable<AnnouncementOfAnIdesLocalApi> Announcements() =>
      Directory.Exists(_folderOfTheirAnnouncements)
         ? Directory.EnumerateFiles(_folderOfTheirAnnouncements, "*.json").Select(AnnouncementOfAnIdesLocalApi.In).OfType<AnnouncementOfAnIdesLocalApi>()
         : [];

   static async Task<IReadOnlyList<IdeWindow>> WindowsListedBy(AnnouncementOfAnIdesLocalApi ide, CancellationToken cancellation)
   {
      try
      {
         var listed = await Http.GetFromJsonAsync<WindowsAnIdeListed>(ide.AddressOf("windows"), JsonSerializerOptions.Web, cancellation);
         return [.. listed!.Windows.Select(it => it.AsIdeWindow)];
      }
      // The announcements' contract: an IDE that doesn't answer isn't there, as when another process got a crashed IDE's process id.
      catch(HttpRequestException notThere) when(notThere.InnerException is SocketException)
      {
         return [];
      }
      catch(TaskCanceledException tooSlow) when(tooSlow.InnerException is TimeoutException)
      {
         return [];
      }
   }
}
