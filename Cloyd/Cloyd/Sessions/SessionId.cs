namespace Cloyd.Sessions;

/// <summary>
/// The id an agent CLI gives a session, such as Claude Code's <see cref="ClaudeCode.ClaudeCodeSessionId"/>. Equal only to
/// the same agent CLI's id with the same <see cref="Value"/>: each CLI gives its own ids, so two CLIs' ids name two
/// sessions, whatever they read.
/// </summary>
/// <param name="Value">The id as the agent CLI writes it, such as <c>1d24f54b-3002-4132-8a65-4d7dc8977525</c>.</param>
abstract record SessionId(string Value)
{
   public sealed override string ToString() => Value;
}
