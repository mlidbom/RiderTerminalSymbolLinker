using Cloyd.Projects;

namespace Cloyd.Hub.Worktrees;

/// <summary>The worktree's saved sessions and live occupants as seen by the hub. Only a
/// preview containing exclusively stopped sessions can be confirmed for removal.</summary>
sealed record WorktreeSessionsPreview(Guid OperationId, string WorktreePath, WorktreeSession[] Sessions, WorktreeShell[] ShellOnlyHosts,
                                     RemovalBlocker[] Blockers)
{
   public WorktreeSubagent[] Descendants { get; init; } = [];
   public bool Empty => Sessions.Length == 0 && ShellOnlyHosts.Length == 0;
   public bool HasOpenTabsOrRunningSessions => ShellOnlyHosts.Length > 0 || Sessions.Any(it => it.IsOpen);

   public void RequireIdentityOf(Project checkout)
   {
      if(OperationId == Guid.Empty || !Path.IsPathFullyQualified(WorktreePath) || !new Project(WorktreePath).Equals(checkout))
         throw new InvalidDataException("The hub returned a preview for a different worktree.");
      if(Sessions.Any(it => string.IsNullOrWhiteSpace(it.AgentCli) || string.IsNullOrWhiteSpace(it.SessionId)) ||
         Sessions.DistinctBy(it => (it.AgentCli, it.SessionId)).Count() != Sessions.Length)
         throw new InvalidDataException("The hub returned an incomplete or repeated session in this worktree.");
   }

   public void RequireReadyFor(Project checkout)
   {
      RequireIdentityOf(checkout);
      if(HasOpenTabsOrRunningSessions)
      {
         var occupants = Sessions.Where(it => it.IsOpen).Select(it => $"{it.AgentCli} {it.SessionId}")
            .Concat(ShellOnlyHosts.Select(it => $"shell {it.HostId}"));
         throw new InvalidOperationException($"Close all session tabs and stop all running sessions in {checkout} ({string.Join(", ", occupants)}).");
      }
      if(Blockers.Length > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, Blockers.Select(it => it.Message)));
      if(Descendants.FirstOrDefault(it => it.BlocksRemoval) is {} helper)
         throw new InvalidOperationException(helper.Reason ?? $"{helper.DisplayName} blocks removal ({helper.Activity}).");
   }

}

sealed record WorktreeSession(string AgentCli, string SessionId, string? HostId, string Activity)
{
   public bool IsOpen => HostId is not null || Activity != "closed";
}
sealed record WorktreeShell(string HostId);
sealed record RemovalBlocker(string Message, string? Code = null, string? SessionId = null);
