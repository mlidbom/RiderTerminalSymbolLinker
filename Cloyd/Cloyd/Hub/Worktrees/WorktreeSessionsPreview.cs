using Cloyd.Projects;

namespace Cloyd.Hub.Worktrees;

/// <summary>The worktree's saved sessions and live occupants as seen by the hub. Only a
/// preview containing exclusively stopped sessions can be confirmed for removal.</summary>
sealed record WorktreeSessionsPreview(Guid OperationId, string WorktreePath, WorktreeSession[] Sessions, WorktreeShell[] ShellOnlyHosts, RemovalBlocker[] Blockers)
{
   public bool Empty => Sessions.Length == 0 && ShellOnlyHosts.Length == 0;
   public bool HasOpenTabsOrRunningSessions => ShellOnlyHosts.Length > 0 || Sessions.Any(it => it.IsOpen);

   public void RequireReadyFor(Project checkout)
   {
      if(OperationId == Guid.Empty || !Path.IsPathFullyQualified(WorktreePath) || !new Project(WorktreePath).Equals(checkout))
         throw new InvalidDataException("The hub returned a preview for a different worktree.");
      if(HasOpenTabsOrRunningSessions)
      {
         var occupants = Sessions.Where(it => it.IsOpen).Select(it => $"{it.AgentCli} {it.SessionId}")
            .Concat(ShellOnlyHosts.Select(it => $"shell {it.HostId}"));
         throw new InvalidOperationException($"Close all session tabs and stop all running sessions in {checkout} ({string.Join(", ", occupants)}).");
      }
      if(Sessions.Any(it => string.IsNullOrWhiteSpace(it.AgentCli) || string.IsNullOrWhiteSpace(it.SessionId)) ||
         Sessions.DistinctBy(it => (it.AgentCli, it.SessionId)).Count() != Sessions.Length)
         throw new InvalidDataException("The hub returned an incomplete or repeated session in this worktree.");
      if(Blockers.Length > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, Blockers.Select(it => it.Message)));
   }

   public string DescribeRemoval()
   {
      var conversations = Sessions.Length == 1 ? "one saved conversation" : $"{Sessions.Length} saved conversations";
      return $"Remove {WorktreePath}?\n\nThis worktree has {conversations}. Archive keeps conversations until you restore them. Delete allows recovery for one week.";
   }
}

sealed record WorktreeSession(string AgentCli, string SessionId, string? HostId, string Activity)
{
   public bool IsOpen => HostId is not null || Activity != "closed";
}
sealed record WorktreeShell(string HostId);
sealed record RemovalBlocker(string Message);
