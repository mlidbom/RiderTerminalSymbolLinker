using ClaudeTooling.Hub.LocalApi.Contract.TypingIntoRunningSessions;
using Cloyd.Codex;

namespace Cloyd.Hub.Commands;

/// <summary>
/// The Codex that the hub types into a session host's shell, quoted as that shell reads it: <c>codex</c> for a new
/// session, or <c>codex resume &lt;id&gt;</c>.
/// </summary>
/// <remarks>
/// Codex takes no name at its start: the hub names a new session that is to have one once Codex shows the session's id.
/// </remarks>
sealed class CodexCommand : AgentCliCommand
{
   /// <summary>The command, as a human would type it, before the shell's quoting: for what Cloyd tells.</summary>
   readonly string _asTyped;

   CodexCommand(CodexToStartInTheRequest request, string asTyped)
   {
      Request = request;
      _asTyped = asTyped;
   }

   /// <summary>What the hub's <c>POST /shells/{processId}/start-codex</c> is asked with.</summary>
   public CodexToStartInTheRequest Request { get; }

   /// <summary>A new session: named <paramref name="name"/> by the hub, or with no name when it is null.</summary>
   public static CodexCommand OfANewSession(string? name) => new(new CodexToStartInTheRequest(name, SessionToResume: null), "codex");

   /// <summary>The session <paramref name="session"/>, continued where it was left.</summary>
   public static CodexCommand Resuming(CodexSession session) =>
      new(new CodexToStartInTheRequest(Name: null, session.Id.Value), $"codex resume {session.Id}");

   /// <summary>Has <paramref name="hub"/> type it, through <see cref="TheHub.StartCodexIn"/>.</summary>
   public override Task HaveTheHubTypeItInto(SessionHostOfTheHub host, TheHub hub, CancellationToken cancellation) => hub.StartCodexIn(host, this, cancellation);

   public override string ToString() => _asTyped;
}
