namespace Cloyd.Hub;

/// <summary>
/// What a running Codex is doing, as the hub's <c>GET /running-sessions</c> tells it: working at a turn, waiting for
/// Magnus, as for his approval of a command, idle, or failed.
/// </summary>
/// <param name="AsReported">As the hub names it: <c>busy</c>, <c>waiting</c>, <c>idle</c> or <c>error</c>. Values may be added.</param>
sealed record StatusOfACodex(string AsReported)
{
   /// <summary>Whether it works at a turn.</summary>
   public bool IsBusy => AsReported == "busy";
}
