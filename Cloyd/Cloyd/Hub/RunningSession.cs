using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;

namespace Cloyd.Hub;

/// <summary>A Claude Code that runs, as the hub's <c>GET /running-sessions</c> lists it: the fields Cloyd uses.</summary>
/// <param name="Process">The process the Claude Code runs as: the same across a <c>/clear</c>.</param>
/// <param name="SessionId">The session the Claude Code runs now.</param>
/// <param name="Name">The name Claude Code shows for the session: null when it has none.</param>
/// <param name="SessionHost">The session host whose console the Claude Code runs in: null when it runs in none, as in a terminal of its own.</param>
sealed record RunningSession(ClaudeCodeProcessId Process, ClaudeSessionId SessionId, string? Name, SessionHostId? SessionHost)
{
   /// <summary>The Claude Code as the hub's answer <paramref name="session"/> lists it.</summary>
   public static RunningSession InThe(RunningSessionInTheAnswer session) =>
      new(new ClaudeCodeProcessId(session.ProcessId), new ClaudeSessionId(session.SessionId), session.Name, session.SessionHost is {} host ? new SessionHostId(host) : null);

   /// <summary>The session the Claude Code runs.</summary>
   public ClaudeSession Session => new(SessionId, Name);
}
