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
///   new session, as Claude Code's does, and <c>/exit</c> ends it, deleting its record. The conversations are kept in
///   <c>fake-conversations</c> in the same folder.
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
               File.AppendAllLines(ConversationFile(session), [typed]);
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

   static string ConversationFile(string session)
   {
      var folder = Path.Combine(ClaudeCodeFolder, "fake-conversations");
      Directory.CreateDirectory(folder);
      return Path.Combine(folder, session + ".txt");
   }

   static IEnumerable<string> ConversationOf(string session) =>
      File.Exists(ConversationFile(session)) ? File.ReadAllLines(ConversationFile(session)) : [];
}
