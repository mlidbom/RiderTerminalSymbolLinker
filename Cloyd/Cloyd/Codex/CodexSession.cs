using Cloyd.Hub;
using Cloyd.Sessions;

namespace Cloyd.Codex;

/// <summary>A Codex session, as a tab knows it: its id, which <c>codex resume</c> takes, and its name.</summary>
/// <param name="Id">The id Codex gave the session.</param>
/// <param name="Name">The name the session goes by: null while it has none.</param>
sealed record CodexSession(CodexSessionId Id, string? Name) : Session(Name)
{
   public override CodexSessionId Id { get; } = Id;

   /// <summary><c>codex resume &lt;its id&gt;</c>.</summary>
   public override CodexCommand CommandThatResumesIt => CodexCommand.Resuming(this);
}
