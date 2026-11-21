using Cloyd.Hub;
using Cloyd.Sessions;

namespace Cloyd.ClaudeCode;

/// <summary>A Claude Code session, as a tab knows it: its id, which <c>claude --resume</c> takes, and its name.</summary>
/// <param name="Id">The id Claude Code gave the session.</param>
/// <param name="Name">The name Claude Code shows for the session: null while it has none.</param>
sealed record ClaudeCodeSession(ClaudeCodeSessionId Id, string? Name) : Session(Name)
{
   public override ClaudeCodeSessionId Id { get; } = Id;

   /// <summary><c>claude --resume &lt;its id&gt;</c>.</summary>
   public override ClaudeCodeCommand CommandThatResumesIt => ClaudeCodeCommand.Resuming(this);
}
