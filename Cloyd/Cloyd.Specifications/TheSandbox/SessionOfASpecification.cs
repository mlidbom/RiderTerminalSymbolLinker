using System.Text.Json.Nodes;
using Cloyd.ClaudeCode;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// A session as if Magnus had run it with Claude Code in <paramref name="Directory"/>, giving it <paramref name="Prompt"/>:
/// its transcript, which <see cref="WriteItsTranscriptFor"/> writes into a sandbox hub's Claude Code folder, as Claude Code
/// 2.1.328 writes one, cut down to what the hub reads for <c>GET /sessions</c> and what the stand-in for Claude Code reads
/// to resume it.
/// </summary>
/// <param name="Id">The session's id.</param>
/// <param name="Directory">The directory it ran in.</param>
/// <param name="Prompt">The one prompt Magnus gave it.</param>
sealed record SessionOfASpecification(ClaudeCodeSessionId Id, string Directory, string Prompt)
{
   /// <summary>The title Magnus gave it with <c>/rename</c>: null for none.</summary>
   public string? Title { get; init; }

   /// <summary>The branch checked out in <see cref="Directory"/> as it ran.</summary>
   public string Branch { get; init; } = "main";

   /// <summary>How long before it is written it was last used: what its transcript's time of writing is set to.</summary>
   public TimeSpan LastUsedAgo { get; init; } = TimeSpan.FromHours(1);

   /// <summary>Whether a tool ran it with <c>claude -p</c>, rather than Magnus in a console.</summary>
   public bool RunWithClaudeP { get; init; }

   /// <summary>Whether it has a conversation to resume: one that has none ran only Claude Code's own commands, such as a cancelled <c>/resume</c>.</summary>
   public bool HasAConversation { get; init; } = true;

   /// <summary>Writes its transcript into the Claude Code folder <paramref name="hub"/> serves, dated <see cref="LastUsedAgo"/>.</summary>
   public void WriteItsTranscriptFor(SandboxHub hub)
   {
      var transcript = hub.TranscriptOf(Id, Directory);
      transcript.Directory!.Create();
      File.WriteAllLines(transcript.FullName, Lines());
      File.SetLastWriteTimeUtc(transcript.FullName, DateTime.UtcNow - LastUsedAgo);
   }

   string[] Lines()
   {
      string[] conversation = HasAConversation ? [ThePrompt(), TheAnswer()] : [AClearing()];
      return Title is null ? conversation : [.. conversation, Line(new JsonObject { ["type"] = "custom-title", ["customTitle"] = Title, ["sessionId"] = Id.Value })];
   }

   /// <summary>The prompt: typed, Claude Code marks it as of <c>human</c> origin; given with <c>claude -p</c>, it has no origin.</summary>
   string ThePrompt()
   {
      var prompt = new JsonObject
      {
         ["parentUuid"] = null,
         ["isSidechain"] = false,
         ["promptId"] = UuidOf(9),
         ["type"] = "user",
         ["message"] = new JsonObject { ["role"] = "user", ["content"] = Prompt },
         ["uuid"] = UuidOf(1),
         ["timestamp"] = "2026-11-20T08:00:00.000Z",
         ["entrypoint"] = RunWithClaudeP ? "sdk-cli" : "cli"
      };
      if(!RunWithClaudeP)
      {
         prompt["origin"] = new JsonObject { ["kind"] = "human" };
         prompt["promptSource"] = "typed";
      }

      return Line(WrittenHere(prompt));
   }

   string TheAnswer() => Line(WrittenHere(new JsonObject
   {
      ["parentUuid"] = UuidOf(1),
      ["isSidechain"] = false,
      ["type"] = "assistant",
      ["message"] = new JsonObject { ["model"] = "claude-opus-5-5", ["role"] = "assistant", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "Done." }) },
      ["uuid"] = UuidOf(2),
      ["timestamp"] = "2026-11-20T08:00:05.000Z"
   }));

   /// <summary>The output of a <c>/clear</c>, which Claude Code writes in a session of no conversation.</summary>
   string AClearing() => Line(WrittenHere(new JsonObject
   {
      ["parentUuid"] = null,
      ["isSidechain"] = false,
      ["type"] = "system",
      ["subtype"] = "local_command",
      ["content"] = "<local-command-stdout></local-command-stdout>",
      ["uuid"] = UuidOf(3),
      ["timestamp"] = "2026-11-20T08:00:00.000Z"
   }));

   /// <summary><paramref name="entry"/> with where it was written: the directory, the session, Claude Code's version and the branch.</summary>
   JsonObject WrittenHere(JsonObject entry)
   {
      entry["cwd"] = Directory;
      entry["sessionId"] = Id.Value;
      entry["version"] = "2.1.328";
      entry["gitBranch"] = Branch;
      return entry;
   }

   /// <summary>The uuid of the session's entry <paramref name="number"/>: the session's id, but for its last digits.</summary>
   string UuidOf(int number) => $"{Id.Value[..^4]}{number:0000}";

   static string Line(JsonObject entry) => entry.ToJsonString();
}
