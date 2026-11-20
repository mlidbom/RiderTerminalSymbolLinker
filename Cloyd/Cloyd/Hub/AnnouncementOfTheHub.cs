using System.Text.Json.Nodes;
using Cloyd.ClaudeCode;

namespace Cloyd.Hub;

/// <summary>
/// What a running hub writes as <c>local-api.json</c> once its local API listens: the protocol it speaks, the port of
/// <c>127.0.0.1</c> it listens on, the token every request carries, and the Claude Code folder it serves. Each hub that
/// starts, as at each publish of the tooling, writes one of its own.
/// </summary>
/// <param name="Protocol">The version of the hub's protocol: raised only for a change that a reader of the one before would get wrong.</param>
/// <param name="Port">The port of <c>127.0.0.1</c> the hub's local API listens on.</param>
/// <param name="Token">What every request carries, as <c>Authorization: Bearer &lt;token&gt;</c>.</param>
/// <param name="ClaudeCodeFolderItServes">The folder of the Claude Codes the hub knows of, and starts in its session hosts: where their sessions' transcripts are.</param>
sealed record AnnouncementOfTheHub(int Protocol, int Port, string Token, ClaudeCodeFolder ClaudeCodeFolderItServes)
{
   const string FileName = "local-api.json";

   /// <summary>The address of <paramref name="path"/>, such as <c>session-hosts</c>, at the hub this announces.</summary>
   public Uri AddressOf(string path) => new($"http://127.0.0.1:{Port}/{path}");

   /// <summary>
   /// The announcement in <paramref name="folder"/>: null while there is none, as while no hub runs, and for a few tenths
   /// of a second at each publish, between the old hub's going and the new one's start.
   /// </summary>
   public static AnnouncementOfTheHub? In(DirectoryInfo folder)
   {
      string json;
      try
      {
         json = File.ReadAllText(Path.Combine(folder.FullName, FileName));
      }
      catch(Exception failure) when(failure is FileNotFoundException or DirectoryNotFoundException)
      {
         return null; // The hub deletes its announcement as it stops, and writes it whole by renaming it into place.
      }

      var announcement = JsonNode.Parse(json)!;
      return new((int)announcement["protocol"]!,
                 (int)announcement["port"]!,
                 (string)announcement["token"]!,
                 new ClaudeCodeFolder(new DirectoryInfo((string)announcement["claudeCodeFolder"]!)));
   }
}
