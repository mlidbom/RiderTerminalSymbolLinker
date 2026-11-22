using Cloyd.Chat.Writing;

namespace Cloyd.Codex.Chat;

/// <summary>Slash commands interpreted by Codex's terminal composer. The app-server takes ordinary messages;
/// handing it a terminal command such as <c>/new</c> would send the words to the model.</summary>
static class CodexTerminalCommand
{
   /// <summary>The built-in command names from Codex 0.171.0's <c>tui/src/slash_command.rs</c>.
   /// Other input starting with a slash, including absolute paths, can be sent as ordinary text.</summary>
   static readonly HashSet<string> Names = new(StringComparer.Ordinal)
   {
      "/model", "/presets", "/daybreak", "/ide", "/permissions", "/keymap", "/vim", "/setup-default-sandbox",
      "/experimental", "/approve", "/memories", "/skills", "/import", "/hooks", "/review", "/rename", "/new",
      "/archive", "/delete", "/resume", "/fork", "/worktree", "/app", "/init", "/compact", "/config", "/recap",
      "/plan", "/voice", "/goal", "/agents", "/side", "/btw", "/copy", "/export", "/raw", "/tui", "/diff",
      "/mention", "/status", "/daemon", "/rc", "/remote", "/warnings", "/cd", "/pwd", "/cwd", "/usage",
      "/debug-config", "/title", "/statusline", "/theme", "/pets", "/pet", "/mcp", "/apps", "/plugins",
      "/logout", "/quit", "/exit", "/feedback", "/rollout", "/ps", "/stop", "/clean", "/clear", "/test-approval",
      "/subagents", "/debug-m-drop", "/debug-m-update"
   };

   public static bool WasTypedIn(DraftOfAMessage draft)
   {
      if(draft.Parts is not [PartOfAMessage.TypedText { Text: ['/', ..] }, ..]) return false;
      var text = draft.Document.Text;
      var end = text.AsSpan().IndexOfAny(" \t\r\n");
      return Names.Contains(end < 0 ? text : text[..end]);
   }
}
