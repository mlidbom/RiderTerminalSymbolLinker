using Cloyd.Chat;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Codex.Chat.Status;

namespace Cloyd.Codex.Chat;

/// <summary>What the chat can draw after one ordered update from the hub. A fresh subscription begins
/// with the session's history and the identity of any turn already under way.</summary>
sealed record CodexChatReading(IReadOnlyList<ShownInTheChat> Rows, CodexTurnProgress? Progress, CodexChatFooter? Footer = null)
{
   public CodexTurnId? TurnUnderWay => Progress?.Id;
}
