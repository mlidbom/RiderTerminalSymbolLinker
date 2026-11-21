using ClaudeTooling.Hub.LocalApi.Contract.TypingIntoRunningSessions;
using Cloyd.ClaudeCode;

namespace Cloyd.Hub;

/// <summary>
/// The Claude Code that the hub types into a session host's shell, quoted as that shell reads it: <c>claude</c>, or
/// <c>claude --name &lt;name&gt;</c> for a new session, or <c>claude --resume &lt;id&gt;</c>.
/// </summary>
sealed class ClaudeCodeCommand : AgentCliCommand
{
   /// <summary>The command, as a human would type it, before the shell's quoting: for what Cloyd tells.</summary>
   readonly string _asTyped;

   ClaudeCodeCommand(ClaudeCodeToStartInTheRequest request, string asTyped)
   {
      Request = request;
      _asTyped = asTyped;
   }

   /// <summary>What the hub's <c>POST /shells/{processId}/start-claude-code</c> is asked with.</summary>
   public ClaudeCodeToStartInTheRequest Request { get; }

   /// <summary>A new session: named <paramref name="name"/>, or with no name when it is null.</summary>
   public static ClaudeCodeCommand OfANewSession(string? name) =>
      new(new ClaudeCodeToStartInTheRequest(name, SessionToResume: null), name is null ? "claude" : $"claude --name {name}");

   /// <summary>The session <paramref name="session"/>, continued from its transcript.</summary>
   public static ClaudeCodeCommand Resuming(ClaudeCodeSession session) =>
      new(new ClaudeCodeToStartInTheRequest(Name: null, session.Id.Value), $"claude --resume {session.Id}");

   /// <summary>Has <paramref name="hub"/> type it, through <see cref="TheHub.StartClaudeCodeIn"/>.</summary>
   public override Task HaveTheHubTypeItInto(SessionHostOfTheHub host, TheHub hub, CancellationToken cancellation) => hub.StartClaudeCodeIn(host, this, cancellation);

   public override string ToString() => _asTyped;
}
