using Cloyd.Chat.Writing;

namespace Cloyd.Codex.Chat;

/// <summary>The parts of the hub's local API a Codex chat uses. Each subscription begins with an authoritative
/// reading, including an empty conversation when the tab has just started Codex.</summary>
interface ICodexChatInTheHub
{
   IAsyncEnumerable<CodexChatReading?> Follow(CodexSessionId session, CancellationToken cancellation);
   Task Send(CodexSessionId session, MessageFromTheChat message, CodexTurnId? steerInto);
   Task Interrupt(CodexSessionId session, CodexTurnId turn);
}
