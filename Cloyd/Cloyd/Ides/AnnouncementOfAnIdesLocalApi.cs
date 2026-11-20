using System.Diagnostics;
using System.Text.Json;

namespace Cloyd.Ides;

/// <summary>
/// The file in which a JetBrains IDE that runs Claude Sessions announces its local API: <c>&lt;its process id&gt;.json</c>,
/// <c>{"protocol", "processId", "port", "token"}</c>, written whole, by a rename, as the API starts, and deleted as it
/// stops. Of it Cloyd reads the process and the port.
/// </summary>
sealed record AnnouncementOfAnIdesLocalApi(int ProcessId, int Port)
{
   /// <summary>What <paramref name="file"/> announces: null when its IDE deleted it as it exited, after the folder was listed.</summary>
   public static AnnouncementOfAnIdesLocalApi? In(string file) =>
      new FileWrittenWhole(file).Text() is {} json ? JsonSerializer.Deserialize<AnnouncementOfAnIdesLocalApi>(json, JsonSerializerOptions.Web)! : null;

   /// <summary>Whether the IDE's process runs: a file a crashed IDE left names one that doesn't.</summary>
   public bool ItsIdeRuns
   {
      get
      {
         try
         {
            using var process = Process.GetProcessById(ProcessId);
            return true;
         }
         catch(ArgumentException) // What it throws when no process of the id runs.
         {
            return false;
         }
      }
   }

   /// <summary>The address of <paramref name="path"/> in the API, on the loopback address, which the API asks to be named as the request's <c>Host</c>.</summary>
   public Uri AddressOf(string path) => new($"http://127.0.0.1:{Port}/{path}");
}
