using Cloyd.Sessions;

namespace Cloyd.Codex;

/// <summary>
/// The id Codex gives a session, such as <c>01a1fe6b-c285-7962-a6ae-85f69a3a51f3</c>: what <c>codex resume</c> takes.
/// Codex's own app-server protocol calls a session a thread, and this its thread id.
/// </summary>
sealed record CodexSessionId(string Value) : SessionId(Value);
