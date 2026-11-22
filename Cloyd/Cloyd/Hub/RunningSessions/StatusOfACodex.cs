namespace Cloyd.Hub.RunningSessions;

/// <summary>
/// What a running Codex is doing, as the hub's <c>GET /running-sessions</c> tells it: working at a turn, waiting for
/// Magnus, as for his approval of a command, idle, or failed.
/// </summary>
/// <param name="AsReported">As the hub names it: <c>busy</c>, <c>waiting</c>, <c>idle</c> or <c>error</c>. Values may be added.</param>
/// <param name="WaitingFor">The reason when Codex is waiting, such as approval or user input.</param>
sealed record StatusOfACodex(string AsReported, string? WaitingFor = null)
{
   /// <summary>Whether it works at a turn.</summary>
   public bool IsBusy => AsReported == "busy";

   public bool IsWaiting => AsReported == "waiting";
}
