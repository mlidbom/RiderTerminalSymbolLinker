namespace CloydSpike.Hub;

/// <summary>A running Claude Code, as <c>GET /running-sessions</c> lists it: the fields the spike uses.</summary>
sealed record RunningSession(int ProcessId, string SessionId, string Status, string? WaitingFor, string? Name, string Directory, string? SessionHost)
{
   public override string ToString() => $"{Name ?? SessionId[..8]} · {Status} · {Directory}";
}
