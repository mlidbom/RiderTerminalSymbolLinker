using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;

namespace Cloyd.Hub;

/// <summary>
/// A running Claude Code's turn under way, as the hub keeps it with each running session: what Claude Code's own spinner
/// line tells, such as "✻ Thinking… (12s · ↓ 1.2k tokens)", before any of it is in the transcript. The hub has it from
/// the <c>tooling-commands</c> mod each Claude Code session loads, which reports the session's main loop: a subagent's
/// turns are left out.
/// </summary>
/// <param name="TurnId">The turn's id, Claude Code's own: what the turn's stream names it by.</param>
/// <param name="StartedAt">When it started, in UTC: what the time it has taken is counted from.</param>
/// <param name="Activity">What it does now.</param>
/// <param name="OutputTokensSoFar">Its output tokens so far, as the spinner's "↓ 1.2k tokens" counts them.</param>
sealed record TurnUnderWay(string TurnId, DateTime StartedAt, ActivityOfATurn Activity, long OutputTokensSoFar)
{
   /// <summary>The turn as the hub's answer <paramref name="turn"/> has it.</summary>
   public static TurnUnderWay InThe(TurnUnderWayInTheAnswer turn) =>
      new(turn.TurnId, turn.StartedAt, ActivityOfATurn.InThe(turn.Activity), turn.OutputTokensSoFar);
}
