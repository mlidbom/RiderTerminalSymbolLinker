using System.Text.RegularExpressions;
using Cloyd.ClaudeCode;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The session whose conversation the pictures of the Claude desktop app in <c>dev_docs/cloyd-chat-specification/</c>
/// show, its transcript with its messages only: <c>the-sample-session.jsonl</c>. In a sandbox hub's Claude Code folder, as
/// if Claude Code had run it there, so that the hub serves its conversation as it serves any session's, for a chat to draw
/// beside the pictures.
/// </summary>
static class TheSampleSession
{
   static readonly ClaudeCodeSessionId Id = new("61edddb4-8a9c-412a-a013-53937bacdc67");

   static readonly string Transcript = Path.Combine(AppContext.BaseDirectory, "TheSampleSession", "the-sample-session.jsonl");

   /// <summary>The id of the session whose conversation is the sample session's, again and again: see <see cref="RepeatedFor"/>.</summary>
   static readonly ClaudeCodeSessionId IdOfTheRepeatedSession = new("0de1a0e5-5a3e-4c0e-9d1e-5e55100f1a90");

   /// <summary>The folder the sample session ran in, its project <c>sample-shop</c>.</summary>
   const string FolderItRanIn = @"C:\sample-shop";

   /// <summary>The session, once its transcript is in the Claude Code folder <paramref name="hub"/> serves.</summary>
   public static ClaudeCodeSessionId For(SandboxHub hub)
   {
      Write(hub, Id, File.ReadAllLines(Transcript));
      return Id;
   }

   /// <summary>
   /// A long conversation, as Magnus's are: the sample session's, <paramref name="times"/> times over, each time after the
   /// last, in a session of its own in the Claude Code folder <paramref name="hub"/> serves.
   /// </summary>
   /// <remarks>
   /// Each time over, every id in the transcript is made its own, but the session's: its entries' <c>uuid</c>s and the
   /// <c>parentUuid</c>s that name them, the ids of Claude's messages and of its tool calls. The first entry of each, which
   /// starts the sample session's line, follows the last entry of the time before.
   /// </remarks>
   public static ClaudeCodeSessionId RepeatedFor(SandboxHub hub, int times)
   {
      var entries = File.ReadAllLines(Transcript);
      var transcript = new List<string>();
      string? lastOfTheTimeBefore = null;
      for(var time = 0; time < times; time++)
         foreach(var entry in entries)
         {
            var itsOwn = EveryIdMadeTheTimes(entry, time).Replace(Id.Value, IdOfTheRepeatedSession.Value, StringComparison.Ordinal);
            if(entry == entries[0] && lastOfTheTimeBefore is not null) itsOwn = itsOwn.Replace("\"parentUuid\": null", $"\"parentUuid\": \"{lastOfTheTimeBefore}\"", StringComparison.Ordinal);
            transcript.Add(itsOwn);
            if(entry == entries[^1]) lastOfTheTimeBefore = UuidOf(itsOwn);
         }

      Write(hub, IdOfTheRepeatedSession, transcript);
      return IdOfTheRepeatedSession;
   }

   /// <summary>
   /// <paramref name="entry"/> with every id in it made that of the time over <paramref name="time"/>: a <c>uuid</c>'s
   /// first eight digits replaced by the time's number, and a message's or tool call's id followed by it. The session's
   /// id is left as it is.
   /// </summary>
   static string EveryIdMadeTheTimes(string entry, int time) =>
      Regex.Replace(entry, """[0-9a-f]{8}(-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})|\b((msg|toolu)_[0-9A-Za-z]+)""",
                    it => it.Value.StartsWith(Id.Value, StringComparison.Ordinal) ? it.Value
                       : it.Groups[1].Success ? $"{time:x8}{it.Groups[1].Value}"
                       : $"{it.Groups[2].Value}_{time}");

   static string UuidOf(string entry) => Regex.Match(entry, "\"uuid\": \"([^\"]+)\"").Groups[1].Value;

   /// <summary>Writes <paramref name="entries"/> as the transcript of <paramref name="session"/> in the Claude Code folder <paramref name="hub"/> serves.</summary>
   static void Write(SandboxHub hub, ClaudeCodeSessionId session, IReadOnlyList<string> entries)
   {
      var transcript = hub.TranscriptOf(session, FolderItRanIn);
      transcript.Directory!.Create();
      File.WriteAllLines(transcript.FullName, entries);
   }
}
