using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cloyd.LocalApi;

/// <summary>
/// The file that tells a client where the Cloyd application's local API listens: <see cref="FileName"/> in
/// <see cref="CloydsFolder"/>, naming the protocol, the application's process, the port of <c>127.0.0.1</c> and the token
/// every request carries, as the plugin's and the hub's announcements do.
/// </summary>
/// <remarks>
/// The application writes it once its API listens, whole at once, so that a reader never finds half of it, and deletes it
/// as the API stops. One left by a crash names a port nothing listens on: a refused connection tells a client that no
/// application runs.
/// </remarks>
/// <param name="Protocol">The <see cref="CloydsLocalApiProtocol.Version"/> the application speaks.</param>
/// <param name="ProcessId">The application's process.</param>
/// <param name="Port">The port of <c>127.0.0.1</c> the API listens on: a free one, new at each start.</param>
/// <param name="Token">What every request carries, as <c>Authorization: Bearer &lt;token&gt;</c>: new at each start.</param>
sealed record AnnouncementOfCloydsLocalApi([property: JsonPropertyName("protocol")] int Protocol,
                                           [property: JsonPropertyName("processId")] int ProcessId,
                                           [property: JsonPropertyName("port")] int Port,
                                           [property: JsonPropertyName("token")] string Token)
{
   /// <summary>The name of the announcement's file, in <see cref="CloydsFolder"/>.</summary>
   public const string FileName = "local-api.json";

   /// <summary>The address of <paramref name="path"/>, such as <c>windows</c>, at the API this announces.</summary>
   public Uri AddressOf(string path) => new($"http://127.0.0.1:{Port}/{path}");

   /// <summary>The announcement in <paramref name="folder"/>: null while there is none, as while no application runs.</summary>
   public static AnnouncementOfCloydsLocalApi? In(CloydsFolder folder)
   {
      string json;
      try
      {
         json = File.ReadAllText(FileIn(folder));
      }
      catch(Exception failure) when(failure is FileNotFoundException or DirectoryNotFoundException)
      {
         return null; // The application deletes its announcement as it stops, and writes it whole by renaming it into place.
      }

      return JsonSerializer.Deserialize<AnnouncementOfCloydsLocalApi>(json, CloydsLocalApiProtocol.Json);
   }

   /// <summary>Writes the announcement in <paramref name="folder"/>, whole at once.</summary>
   public void WriteIn(CloydsFolder folder)
   {
      folder.Folder.Create();
      var writing = $"{FileIn(folder)}.writing";
      File.WriteAllText(writing, JsonSerializer.Serialize(this, CloydsLocalApiProtocol.Json));
      File.Move(writing, FileIn(folder), overwrite: true);
   }

   /// <summary>Deletes the announcement in <paramref name="folder"/>, as the API stops.</summary>
   public static void WithdrawFrom(CloydsFolder folder) => File.Delete(FileIn(folder));

   static string FileIn(CloydsFolder folder) => Path.Combine(folder.Folder.FullName, FileName);
}
