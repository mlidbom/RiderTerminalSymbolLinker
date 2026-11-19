using System.Text.Json;
using System.Text.Json.Nodes;

namespace FakeClaudeCode;

/// <summary>
///   A stand-in for Claude Code, the <c>claude</c> command, that the sandbox's Claude Sessions runs instead of it: it
///   records its session as Claude Code does, as <c>sessions/&lt;process id&gt;.json</c> in the folder
///   <c>CLAUDE_CONFIG_DIR</c> names, and keeps its transcript as Claude Code does, and does nothing else. So the
///   specifications of Claude Sessions run in seconds, cost nothing, and leave nothing in the user's own Claude Code
///   folder.
/// </summary>
/// <remarks>
///   <c>claude</c> starts a new session, <c>claude --name &lt;name&gt;</c> starts one with a name, as <c>/rename</c>
///   gives it, and <c>claude --resume &lt;id&gt;</c> resumes one, from whichever folder it ran in before; each says
///   which it did, with the session's id, and then the lines its conversation had. Typed lines are
///   its conversation: <c>/clear</c> starts a new session, as Claude Code's does, <c>/rename &lt;name&gt;</c> names it,
///   and <c>/exit</c> ends it, deleting its record.
///   <br/>It keeps each session's transcript where Claude Code does, as <c>projects/&lt;a name made from the folder it
///   started in&gt;/&lt;session id&gt;.jsonl</c> in the same folder, and in Claude Code's format, the entries Claude
///   Sessions reads: for each line typed, a <c>user</c> entry with the folder it runs in, and a <c>last-prompt</c>; after
///   the first, an <c>ai-title</c>, the title Claude Code gives a session, made from that line; and for
///   <c>/rename</c>, a <c>custom-title</c>.
/// </remarks>
static class Program
{
   static readonly string ClaudeCodeFolder = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")
                                          ?? throw new InvalidOperationException("The fake Claude Code runs only where CLAUDE_CONFIG_DIR names its folder: in the sandbox.");

   static string RecordFile => Path.Combine(ClaudeCodeFolder, "sessions", $"{Environment.ProcessId}.json");

   static string ProjectsFolder => Path.Combine(ClaudeCodeFolder, "projects");

   static int Main(string[] arguments)
   {
      var session = arguments is ["--resume", var id] ? id : NewSession();
      Console.WriteLine(arguments is ["--resume", _] ? $"Resumed session {session}" : $"New session {session}");
      foreach(var line in ConversationOf(session)) Console.WriteLine($"> {line}");
      if(arguments is ["--name", var given]) Rename(session, given);
      var name = NameOf(session);
      Record(session, name);

      while(Console.ReadLine() is {} typed)
      {
         switch(typed.Trim())
         {
            case "/exit":
               File.Delete(RecordFile);
               return 0;
            case "/clear":
               session = NewSession();
               name = null;
               Console.WriteLine($"New session {session}");
               Record(session, name);
               break;
            case var command when command.StartsWith("/rename "):
               name = command["/rename ".Length..].Trim();
               Rename(session, name);
               Record(session, name);
               Console.WriteLine($"Renamed to {name}");
               break;
            default:
               Say(session, typed);
               Console.WriteLine($"> {typed}");
               break;
         }
      }

      return 0;
   }

   static string NewSession() => Guid.NewGuid().ToString();

   /// <summary>Names the session, as Claude Code's <c>/rename</c> does: with a <c>custom-title</c> in its transcript.</summary>
   static void Rename(string session, string name)
   {
      AddToTheTranscript(session, new JsonObject { ["type"] = "custom-title", ["customTitle"] = name, ["sessionId"] = session });
   }

   /// <summary>Writes the session's record as Claude Code does, the parts Claude Sessions reads.</summary>
   static void Record(string session, string? name)
   {
      Directory.CreateDirectory(Path.GetDirectoryName(RecordFile)!);
      File.WriteAllText(RecordFile,
                        JsonSerializer.Serialize(new Dictionary<string, object?>
                        {
                           ["pid"] = Environment.ProcessId,
                           ["startedAt"] = new DateTimeOffset(System.Diagnostics.Process.GetCurrentProcess().StartTime).ToUnixTimeMilliseconds(),
                           ["sessionId"] = session,
                           ["cwd"] = Environment.CurrentDirectory,
                           ["name"] = name ?? "fake-" + session[..8]
                        }));
   }

   static void Say(string session, string typed)
   {
      var isTheFirstLine = !ConversationOf(session).Any();
      AddToTheTranscript(session, new JsonObject
      {
         ["type"] = "user",
         ["isSidechain"] = false,
         ["message"] = new JsonObject { ["role"] = "user", ["content"] = typed },
         ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
         ["entrypoint"] = "cli",
         ["cwd"] = Environment.CurrentDirectory,
         ["sessionId"] = session
      });
      AddToTheTranscript(session, new JsonObject { ["type"] = "last-prompt", ["lastPrompt"] = typed, ["sessionId"] = session });
      if(isTheFirstLine) AddToTheTranscript(session, new JsonObject { ["type"] = "ai-title", ["aiTitle"] = $"About {typed}", ["sessionId"] = session });
   }

   /// <summary>
   ///   The session's transcript: where it is, in the folder of whichever project it started in, as Claude Code finds it
   ///   to resume it, or else where Claude Code starts one for the folder it runs in: that folder's path, with each
   ///   character that is neither a letter nor a digit made a <c>-</c>.
   /// </summary>
   static string TranscriptFile(string session) =>
      (Directory.Exists(ProjectsFolder) ? Directory.GetFiles(ProjectsFolder, session + ".jsonl", SearchOption.AllDirectories).FirstOrDefault() : null)
   ?? Path.Combine(ProjectsFolder, new string([.. Environment.CurrentDirectory.Select(it => char.IsAsciiLetterOrDigit(it) ? it : '-')]), session + ".jsonl");

   static void AddToTheTranscript(string session, JsonObject entry)
   {
      var file = TranscriptFile(session);
      Directory.CreateDirectory(Path.GetDirectoryName(file)!);
      File.AppendAllLines(file, [entry.ToJsonString()]);
   }

   static IEnumerable<JsonNode> EntriesOf(string session) =>
      File.Exists(TranscriptFile(session)) ? File.ReadAllLines(TranscriptFile(session)).Select(line => JsonNode.Parse(line)!) : [];

   static IEnumerable<string> ConversationOf(string session) =>
      EntriesOf(session).Where(it => (string?)it["type"] == "user").Select(it => (string)it["message"]!["content"]!);

   static string? NameOf(string session) =>
      EntriesOf(session).Where(it => (string?)it["type"] == "custom-title").Select(it => (string?)it["customTitle"]).LastOrDefault();
}
