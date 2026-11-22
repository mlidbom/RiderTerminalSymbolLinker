using Cloyd.Chat.Writing;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;

namespace Cloyd.Codex.Chat;

/// <summary>The parts of the hub's local API a Codex chat uses. Each subscription begins with an authoritative
/// reading, including an empty conversation when the tab has just started Codex.</summary>
interface ICodexChatInTheHub
{
   IAsyncEnumerable<CodexChatReading?> Follow(CodexSessionId session, CancellationToken cancellation);
   Task<CodexChatReading> SavedConversation(CodexSessionId session, CancellationToken cancellation);
   /// <summary>Null when the daemon lacks this optional listing. An empty list means the daemon supports it and none run.</summary>
   Task<CodexChatBackgroundTerminals?> BackgroundTerminals(CodexSessionId session, CancellationToken cancellation);
   Task Send(CodexSessionId session, MessageFromTheChat message, CodexTurnId? steerInto);
   Task Interrupt(CodexSessionId session, CodexTurnId turn);
}
