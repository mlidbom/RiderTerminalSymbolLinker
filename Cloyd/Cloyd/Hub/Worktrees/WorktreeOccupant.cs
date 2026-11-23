namespace Cloyd.Hub.Worktrees;

/// <summary>The host, shell and agent process identity that the hub rechecks before stopping an idle agent or closing a shell.</summary>
sealed record OccupantTarget(string? HostId, int? ShellPid, string? AgentCli, string? SessionId, int? AgentPid, DateTime? AgentStartedAt);
sealed record WorktreeOccupant(OccupantTarget Target, string Activity, string? Action, string? Reason, string? Name = null);
sealed record ReviewedOccupants(WorktreeOccupant[] Occupants)
{
   public WorktreeSubagent[] Descendants { get; init; } = [];
   /// <summary>Hosts whose current shell prompt places them outside the reviewed checkout.
   /// Older hubs provide no such evidence.</summary>
   public string[] VerifiedShellsElsewhere { get; init; } = [];
}
sealed record OccupantActionResult(OccupantTarget Target, string Outcome, string? Reason);
sealed record OccupantActionResults(OccupantActionResult[] Results);
