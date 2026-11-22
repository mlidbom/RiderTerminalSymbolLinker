using Cloyd.Chat;
using Cloyd.Codex.Chat;
using Cloyd.Hub.RunningSessions;
using Cloyd.Sessions;

namespace Cloyd.ProjectsDashboard;

/// <summary>Shows the conversation of a running session with no session host. There is no terminal to
/// bring forward, and neither editor nor send actions are offered.</summary>
sealed class ConversationOutsideAHost
{
   public SessionChat Chat { get; }

   public ConversationOutsideAHost(SessionsOfCloyd sessions, RunningSession running)
   {
      Chat = running switch
      {
         RunningClaudeCode => new ChatOfAClaudeCodeSession(sessions.Hub, sessions.Images),
         RunningCodex => new ChatOfACodexSession(sessions.Hub, sessions.Images, new CodexSessionDrafts()),
         _ => throw new ArgumentOutOfRangeException(nameof(running))
      };
      Chat.InputBox.IsVisible = false;
      switch(Chat)
      {
         case ChatOfAClaudeCodeSession claude: claude.WaitingForMagnusInTheTerminal.IsEnabled = false; break;
         case ChatOfACodexSession codex: codex.WaitingForMagnusInTheTerminal.IsEnabled = false; break;
      }
      Update(running);
   }

   public void Update(RunningSession running)
   {
      switch(Chat, running)
      {
         case (ChatOfAClaudeCodeSession chat, RunningClaudeCode claude):
            chat.ClaudeCode = claude;
            chat.Show(claude.SessionId);
            break;
         case (ChatOfACodexSession chat, RunningCodex codex):
            chat.Show(codex.SessionId, codex);
            break;
      }
   }
}
