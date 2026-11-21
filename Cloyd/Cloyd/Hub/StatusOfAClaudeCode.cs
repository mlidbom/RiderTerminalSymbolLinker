namespace Cloyd.Hub;

/// <summary>
/// What a running Claude Code reports it is doing, as the hub's <c>GET /running-sessions</c> tells it: working at a turn,
/// waiting for Magnus in a dialog of its own, such as a permission prompt, or idle.
/// </summary>
/// <param name="AsReported">
/// As Claude Code names it: <c>busy</c> (a turn, a compaction, or a background agent), <c>waiting</c>, <c>shell</c> (idle,
/// with a background shell task running), or <c>idle</c>. Values may be added.
/// </param>
/// <param name="WaitingFor">What it waits for, while <c>waiting</c>, as Claude Code names it, such as "permission prompt": null otherwise.</param>
sealed record StatusOfAClaudeCode(string AsReported, string? WaitingFor)
{
   /// <summary>Whether it works: a turn, a compaction, or a background agent is under way.</summary>
   public bool IsBusy => AsReported == "busy";

   /// <summary>Whether a dialog of its own waits for Magnus, which only its terminal shows, such as a permission prompt.</summary>
   public bool IsWaiting => AsReported == "waiting";

   /// <summary>
   /// Whether it is idle, with background work running: a shell task or a monitor, which may wake it again. How many and
   /// which, and those that run while Claude works, a <see cref="RunningSession.BackgroundWork"/> tells.
   /// </summary>
   public bool IsIdleWithBackgroundWork => AsReported == "shell";
}
