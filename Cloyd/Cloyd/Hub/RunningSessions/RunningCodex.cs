using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Codex;

namespace Cloyd.Hub.RunningSessions;

/// <summary>A Codex that runs, as the hub's <c>GET /running-sessions</c> lists it: the fields Cloyd uses.</summary>
/// <param name="SessionId">The session the Codex runs now.</param>
/// <param name="Name">The name the session goes by: null when it has none.</param>
/// <param name="SessionHost">The session host whose console the Codex runs in: null when it runs in none, as in a terminal of its own.</param>
/// <param name="Status">What the Codex is doing, as the hub tells it: working, waiting for Magnus, idle, or failed.</param>
sealed record RunningCodex(CodexSessionId SessionId, string? Name, SessionHostId? SessionHost, StatusOfACodex Status) : RunningSession(SessionHost)
{
   /// <summary>The Codex as the hub's answer <paramref name="session"/> lists it.</summary>
   public static RunningCodex InThe(RunningSessionInTheAnswer session) =>
      new(new CodexSessionId(session.SessionId), session.Name, session.SessionHost is {} host ? new SessionHostId(host) : null, new StatusOfACodex(session.Status, session.WaitingFor))
      { WorkingFolder = WorkingFolderIn(session) };

   public override CodexSession Session => new(SessionId, Name);

   public override bool IsBusy => Status.IsBusy;
}
