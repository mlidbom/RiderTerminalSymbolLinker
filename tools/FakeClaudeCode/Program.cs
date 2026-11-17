using System.Text.Json;

namespace FakeClaudeCode;

/// <summary>
///   A stand-in for Claude Code, the <c>claude</c> command, that the sandbox's Claude Sessions runs instead of it: it
///   records its session as Claude Code does, as <c>sessions/&lt;process id&gt;.json</c> in the folder
///   <c>CLAUDE_CONFIG_DIR</c> names, and does nothing else. So the specifications of Claude Sessions run in seconds, cost
///   nothing, and leave nothing in the user's own Claude Code folder.
/// </summary>
/// <remarks>
///   <c>claude</c> starts a new session, and <c>claude --resume &lt;id&gt;</c> resumes one; each says which it did, with
///   the session's id, and then the lines its conversation had. Typed lines are its conversation: <c>/clear</c> starts a
///   new session, as Claude Code's does, and <c>/exit</c> ends it, deleting its record. It keeps each session's
///   conversation where Claude Code keeps a session's transcript, as <c>projects/&lt;a name made from its
///   folder&gt;/&lt;session id&gt;.jsonl</c> in the same folder, a line of JSON for each line typed, from the first.
/// </remarks>
static class Program
{
   static readonly string ClaudeCodeFolder = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")
                                          ?? throw new InvalidOperationException("The fake Claude Code runs only where CLAUDE_CONFIG_DIR names its folder: in the sandbox.");

   static string RecordFile => Path.Combine(ClaudeCodeFolder, "sessions", $"{Environment.ProcessId}.json");

   static int Main(string[] arguments)
   {
      var session = arguments is ["--resume", var id] ? id : NewSession();
      Console.WriteLine(arguments is ["--resume", _] ? $"Resumed session {session}" : $"New session {session}");
      foreach(var line in ConversationOf(session)) Console.WriteLine($"> {line}");
      Record(session);

      while(Console.ReadLine() is {} typed)
      {
         switch(typed.Trim())
         {
            case "/exit":
               File.Delete(RecordFile);
               return 0;
            case "/clear":
               session = NewSession();
               Console.WriteLine($"New session {session}");
               Record(session);
               break;
            default:
               AddToTheTranscript(session, typed);
               Console.WriteLine($"> {typed}");
               break;
         }
      }

      return 0;
   }

   static string NewSession() => Guid.NewGuid().ToString();

   /// <summary>Writes the session's record as Claude Code does, the parts Claude Sessions reads.</summary>
   static void Record(string session)
   {
      Directory.CreateDirectory(Path.GetDirectoryName(RecordFile)!);
      File.WriteAllText(RecordFile,
                        JsonSerializer.Serialize(new Dictionary<string, object>
                        {
                           ["pid"] = Environment.ProcessId,
                           ["startedAt"] = new DateTimeOffset(System.Diagnostics.Process.GetCurrentProcess().StartTime).ToUnixTimeMilliseconds(),
                           ["sessionId"] = session,
                           ["cwd"] = Environment.CurrentDirectory,
                           ["name"] = "fake-" + session[..8]
                        }));
   }

   /// <summary>
   ///   The session's transcript, in the folder Claude Code would keep it in for the folder it runs in: its path, with each
   ///   character that is neither a letter nor a digit made a <c>-</c>.
   /// </summary>
   static string TranscriptFile(string session) =>
      Path.Combine(ClaudeCodeFolder, "projects", new string([.. Environment.CurrentDirectory.Select(it => char.IsAsciiLetterOrDigit(it) ? it : '-')]), session + ".jsonl");

   static void AddToTheTranscript(string session, string typed)
   {
      Directory.CreateDirectory(Path.GetDirectoryName(TranscriptFile(session))!);
      File.AppendAllLines(TranscriptFile(session), [JsonSerializer.Serialize(new Dictionary<string, string> { ["typed"] = typed })]);
   }

   static IEnumerable<string> ConversationOf(string session) =>
      File.Exists(TranscriptFile(session))
         ? File.ReadAllLines(TranscriptFile(session)).Select(line => JsonSerializer.Deserialize<Dictionary<string, string>>(line)!["typed"])
         : [];
}
