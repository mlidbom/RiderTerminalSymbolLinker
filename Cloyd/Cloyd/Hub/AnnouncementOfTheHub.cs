using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using Cloyd.ClaudeCode;

namespace Cloyd.Hub;

/// <summary>
/// What a running hub writes as its announcement once its local API listens: the protocol it speaks, the port of
/// <c>127.0.0.1</c> it listens on, the token every request carries, and the Claude Code folder it serves. Each hub that
/// starts, as at each publish of the tooling, writes one of its own.
/// </summary>
/// <param name="Says">The announcement, as the hub's contract has it.</param>
sealed record AnnouncementOfTheHub(WhatTheAnnouncementSays Says)
{
   /// <summary>The version of the hub's protocol: raised only for a change that a reader of the one before would get wrong.</summary>
   public int Protocol => Says.Protocol;

   /// <summary>What every request carries, as <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
   public string Token => Says.Token;

   /// <summary>The folder of the Claude Codes the hub knows of, and starts in its session hosts: where their sessions' transcripts are.</summary>
   public ClaudeCodeFolder ClaudeCodeFolderItServes => new(new DirectoryInfo(Says.ClaudeCodeFolder));

   /// <summary>The address of <paramref name="path"/>, such as <c>session-hosts</c>, at the hub this announces.</summary>
   public Uri AddressOf(string path) => new($"http://127.0.0.1:{Says.Port}/{path}");

   /// <summary>
   /// The announcement in <paramref name="folder"/>: null while there is none, as while no hub runs, and for a few tenths
   /// of a second at each publish, between the old hub's going and the new one's start.
   /// </summary>
   public static AnnouncementOfTheHub? In(DirectoryInfo folder)
   {
      string json;
      try
      {
         json = File.ReadAllText(Path.Combine(folder.FullName, WhatTheAnnouncementSays.FileName));
      }
      catch(Exception failure) when(failure is FileNotFoundException or DirectoryNotFoundException)
      {
         return null; // The hub deletes its announcement as it stops, and writes it whole by renaming it into place.
      }

      return new(JsonSerializer.Deserialize<WhatTheAnnouncementSays>(json, LocalApiProtocol.Json)!);
   }
}
