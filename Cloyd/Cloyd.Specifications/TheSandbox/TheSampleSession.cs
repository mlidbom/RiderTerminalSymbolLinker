using Cloyd.ClaudeCode;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The session whose conversation the pictures of the Claude desktop app in <c>dev_docs/cloyd-chat-specification/</c>
/// show, its transcript with its messages only: <c>the-sample-session.jsonl</c>. In the sandbox hub's Claude Code folder,
/// as if Claude Code had run it there, so that the hub serves its conversation as it serves any session's, for a chat to
/// draw beside the pictures.
/// </summary>
static class TheSampleSession
{
   static readonly ClaudeSessionId Id = new("61edddb4-8a9c-412a-a013-53937bacdc67");

   static readonly string Transcript = Path.Combine(AppContext.BaseDirectory, "TheSampleSession", "the-sample-session.jsonl");

   /// <summary>The session, once its transcript is in the sandbox's Claude Code folder, in a project folder of its own, as its project was <c>sample-shop</c>.</summary>
   public static ClaudeSessionId InTheSandbox()
   {
      var inTheSandbox = new FileInfo(Path.Combine(Sandbox.Started.HubOfTheSandbox.ClaudeCodeFolder, "projects", "C--sample-shop", $"{Id}.jsonl"));
      inTheSandbox.Directory!.Create();
      File.Copy(Transcript, inTheSandbox.FullName, overwrite: true);
      return Id;
   }
}
