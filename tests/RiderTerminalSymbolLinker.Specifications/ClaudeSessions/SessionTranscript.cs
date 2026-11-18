using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

/// <summary>
///   A session's transcript for a specification to write as Claude Code keeps one, the entries Claude Sessions reads:
///   a session that ran in <see cref="Folder"/>, with <see cref="Prompts"/> typed into it, last active
///   <see cref="LastActiveAgo"/>. With no prompts, it has no conversation.
/// </summary>
public sealed record SessionTranscript(string Folder, TimeSpan LastActiveAgo, params IReadOnlyList<string> Prompts)
{
   /// <summary>The session's id, which names its transcript.</summary>
   public string Id { get; } = Guid.NewGuid().ToString();

   /// <summary>The name the user gave the session with <c>/rename</c>, if any.</summary>
   public string? Name { get; init; }

   /// <summary>The title Claude Code gave the session, if any.</summary>
   public string? Title { get; init; }

   /// <summary>The git branch its folder had checked out, if it was in git.</summary>
   public string? GitBranch { get; init; }

   /// <summary>Whether <c>claude -p</c> ran it, as a tool runs a single prompt: Claude Code names it an SDK's session.</summary>
   public bool RunByATool { get; init; }

   /// <summary>
   ///   Writes the transcript into <paramref name="claudeCodeFolder"/>'s <c>projects</c>, in the folder Claude Code
   ///   would name after <see cref="Folder"/>, and dates it <see cref="LastActiveAgo"/>.
   /// </summary>
   public void WriteTo(string claudeCodeFolder)
   {
      var file = Path.Combine(claudeCodeFolder, "projects", new string([.. Folder.Select(it => char.IsAsciiLetterOrDigit(it) ? it : '-')]), Id + ".jsonl");
      Directory.CreateDirectory(Path.GetDirectoryName(file)!);
      File.WriteAllLines(file, [.. Entries().Select(it => it.ToJsonString())]);
      File.SetLastWriteTimeUtc(file, DateTime.UtcNow - LastActiveAgo);
   }

   IEnumerable<JsonObject> Entries()
   {
      yield return new JsonObject { ["type"] = "permission-mode", ["permissionMode"] = "default", ["sessionId"] = Id };
      foreach(var prompt in Prompts)
      {
         yield return new JsonObject
         {
            ["type"] = "user",
            ["isSidechain"] = false,
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = prompt },
            ["timestamp"] = (DateTimeOffset.UtcNow - LastActiveAgo).ToString("O"),
            ["entrypoint"] = RunByATool ? "sdk-cli" : "cli",
            ["cwd"] = Folder,
            ["sessionId"] = Id,
            ["gitBranch"] = GitBranch
         };
         yield return new JsonObject { ["type"] = "last-prompt", ["lastPrompt"] = prompt, ["sessionId"] = Id };
      }
      if(Title is not null) yield return new JsonObject { ["type"] = "ai-title", ["aiTitle"] = Title, ["sessionId"] = Id };
      if(Name is not null) yield return new JsonObject { ["type"] = "custom-title", ["customTitle"] = Name, ["sessionId"] = Id };
   }
}
