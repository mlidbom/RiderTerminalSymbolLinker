using Cloyd.Chat.Status;
using Cloyd.Chat.Writing;
using Cloyd.Hub;

namespace Cloyd.Chat;

/// <summary>The input, conversation and turn shared by every chat that shows the same Claude Code session.
/// The conversation acknowledges messages in this input and supplies tool calls to this turn.</summary>
sealed class ClaudeCodeChatState
{
   public TheHub Hub { get; }
   public ClaudeCodeChatInput Input { get; }
   public ClaudeCodeTurnStatus Turn { get; } = new();
   public ClaudeCodeConversation Conversation { get; }

   public ClaudeCodeChatState(TheHub hub)
   {
      Hub = hub;
      Input = new ClaudeCodeChatInput(hub);
      Conversation = new ClaudeCodeConversation(hub, Input, Turn);
   }
}
