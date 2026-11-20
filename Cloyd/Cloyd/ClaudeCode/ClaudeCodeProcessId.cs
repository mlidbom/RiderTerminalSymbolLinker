namespace Cloyd.ClaudeCode;

/// <summary>
/// The id of the process a Claude Code runs as: <c>claude.exe</c>'s. It stays the same across a <c>/clear</c>, which has
/// the Claude Code go on in a new session, so it finds the Claude Code where a session's id no longer does.
/// </summary>
readonly record struct ClaudeCodeProcessId(int Value)
{
   public override string ToString() => Value.ToString();
}
