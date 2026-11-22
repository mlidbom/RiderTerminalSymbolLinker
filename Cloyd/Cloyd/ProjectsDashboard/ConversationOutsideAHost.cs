using Cloyd.Chat;
using Cloyd.Codex.Chat;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.Hub.RunningSessions;
using Cloyd.Sessions;

namespace Cloyd.ProjectsDashboard;

/// <summary>Opens the read-only conversation of a session outside a host, including the saved
/// conversation of an agent that has stopped.</summary>
sealed class ConversationOutsideAHost
{
   public SessionChat Chat { get; }

   public ConversationOutsideAHost(SessionsOfCloyd sessions, Session session, RunningSession? running)
   {
      Chat = session switch
      {
         ClaudeCodeSession => new ChatOfAClaudeCodeSession(sessions.Hub, sessions.Images),
         CodexSession => new ChatOfACodexSession(sessions.Hub, sessions.Images, new CodexSessionDrafts()),
         _ => throw new ArgumentOutOfRangeException(nameof(session))
      };
      Chat.InputBox.IsVisible = false;
      switch(Chat)
      {
         case ChatOfAClaudeCodeSession claude: claude.WaitingForMagnusInTheTerminal.IsEnabled = false; break;
         case ChatOfACodexSession codex: codex.WaitingForMagnusInTheTerminal.IsEnabled = false; break;
      }
      Update(session, running);
   }

   public void Update(Session session, RunningSession? running)
   {
      switch(Chat, session)
      {
         case (ChatOfAClaudeCodeSession chat, ClaudeCodeSession claude):
            chat.ClaudeCode = running as RunningClaudeCode;
            chat.Show(claude.Id);
            break;
         case (ChatOfACodexSession chat, CodexSession codex):
            chat.Show(codex.Id, running as RunningCodex);
            break;
      }
   }
}
