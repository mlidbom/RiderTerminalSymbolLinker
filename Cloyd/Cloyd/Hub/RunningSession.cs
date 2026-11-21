using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;

namespace Cloyd.Hub;

/// <summary>A Claude Code that runs, as the hub's <c>GET /running-sessions</c> lists it: the fields Cloyd uses.</summary>
/// <param name="Process">The process the Claude Code runs as: the same across a <c>/clear</c>.</param>
/// <param name="SessionId">The session the Claude Code runs now.</param>
/// <param name="Name">The name Claude Code shows for the session: null when it has none.</param>
/// <param name="SessionHost">The session host whose console the Claude Code runs in: null when it runs in none, as in a terminal of its own.</param>
/// <param name="Status">What the Claude Code reports it is doing: working, waiting for Magnus, or idle.</param>
/// <param name="StatusLine">What the Claude Code last showed under its input box: null until it has shown its status line to the hub, which it does within a minute.</param>
/// <param name="TurnUnderWay">
/// What its turn under way does, as its spinner line tells: null while no turn is under way, and in a session that doesn't
/// load the <c>tooling-commands</c> mod, which tells the hub of it.
/// </param>
sealed record RunningSession(ClaudeCodeProcessId Process, ClaudeSessionId SessionId, string? Name, SessionHostId? SessionHost, StatusOfAClaudeCode Status, ClaudeCodesStatusLine? StatusLine, TurnUnderWay? TurnUnderWay)
{
   /// <summary>The Claude Code as the hub's answer <paramref name="session"/> lists it.</summary>
   public static RunningSession InThe(RunningSessionInTheAnswer session) =>
      new(new ClaudeCodeProcessId(session.ProcessId), new ClaudeSessionId(session.SessionId), session.Name, session.SessionHost is {} host ? new SessionHostId(host) : null, new StatusOfAClaudeCode(session.Status, session.WaitingFor),
         session.StatusLine is {} statusLine ? ClaudeCodesStatusLine.InThe(statusLine) : null,
         session.TurnUnderWay is {} turn ? TurnUnderWay.InThe(turn) : null);

   /// <summary>The session the Claude Code runs.</summary>
   public ClaudeSession Session => new(SessionId, Name);
}
