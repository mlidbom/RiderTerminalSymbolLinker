using System.Text.Json.Nodes;
using Cloyd.Codex;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// A session as if Magnus had run it with Codex in <paramref name="Directory"/>, giving it <paramref name="Prompt"/>: the
/// record the stand-in for Codex keeps of it, which <see cref="WriteItsRecordFor"/> writes into a sandbox hub's Codex home,
/// where the hub lists it as Codex lists a session, through the stand-in, and where the stand-in resumes it from.
/// </summary>
/// <remarks>
/// The record's format is the stand-in's (<c>~/.claude/src/StandInForCodex/OnDisk/RecordOfAThread.cs</c>), a contract
/// with these specifications that gpt-collaborators keep: a file of its own for each session, in the home's
/// <c>stand-in-threads</c> folder, in JSON, camel-cased. The hub lists a session with at least one turn, whose first prompt
/// is Codex's preview of it.
/// </remarks>
/// <param name="Id">The session's id.</param>
/// <param name="Directory">The directory it ran in.</param>
/// <param name="Prompt">The one prompt Magnus gave it, its first, which Codex previews it by.</param>
sealed record CodexSessionOfASpecification(CodexSessionId Id, string Directory, string Prompt)
{
   /// <summary>The name Magnus gave it with <c>/rename</c>: null for none.</summary>
   public string? Name { get; init; }

   /// <summary>How long before it is written it was last used: when its record says Codex last changed it.</summary>
   public TimeSpan LastUsedAgo { get; init; } = TimeSpan.FromHours(1);

   /// <summary>Writes its record into the Codex home <paramref name="hub"/> serves, last used <see cref="LastUsedAgo"/>.</summary>
   public void WriteItsRecordFor(SandboxHub hub)
   {
      var folder = new DirectoryInfo(Path.Combine(hub.CodexFolder, "stand-in-threads"));
      folder.Create();
      File.WriteAllText(Path.Combine(folder.FullName, $"{Id.Value}.json"), TheRecord().ToJsonString());
   }

   JsonObject TheRecord()
   {
      var lastUsed = (DateTimeOffset.UtcNow - LastUsedAgo).ToUnixTimeSeconds();
      return new JsonObject
      {
         ["id"] = Id.Value,
         ["name"] = Name,
         ["directory"] = Directory,
         ["branch"] = "main",
         ["createdAt"] = lastUsed - 60,
         ["updatedAt"] = lastUsed,
         ["source"] = "cli",
         ["turns"] = new JsonArray(new JsonObject { ["prompts"] = new JsonArray(Prompt), ["reply"] = $"Heard: {Prompt}", ["status"] = "completed" })
      };
   }
}
