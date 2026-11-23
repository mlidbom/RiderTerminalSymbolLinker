using Cloyd.Projects;

namespace Cloyd.Hub.Worktrees;

/// <summary>The hub's review of every session and shell using a worktree. Its operation ID
/// binds the exact inventory and process identities the hub must recheck before closing them.</summary>
sealed record WorktreeSessionsPreview(Guid OperationId, string WorktreePath, WorktreeSession[] Sessions, WorktreeShell[] ShellOnlyHosts, RemovalBlocker[] Blockers)
{
   public bool Empty => Sessions.Length == 0 && ShellOnlyHosts.Length == 0;
   public HashSet<string> HostIds => ShellOnlyHosts.Select(it => it.HostId).Concat(Sessions.Select(it => it.HostId).OfType<string>()).ToHashSet();

   public void RequireReadyFor(Project checkout)
   {
      if(OperationId == Guid.Empty || !Path.IsPathFullyQualified(WorktreePath) || !new Project(WorktreePath).Equals(checkout))
         throw new InvalidDataException("The hub returned a preview for a different worktree.");
      var hosts = Sessions.Select(it => it.HostId).OfType<string>().Concat(ShellOnlyHosts.Select(it => it.HostId)).ToArray();
      if(Sessions.Any(it => string.IsNullOrWhiteSpace(it.AgentCli) || string.IsNullOrWhiteSpace(it.SessionId)) ||
         Sessions.DistinctBy(it => (it.AgentCli, it.SessionId)).Count() != Sessions.Length ||
         hosts.Any(string.IsNullOrWhiteSpace) || hosts.Distinct().Count() != hosts.Length)
         throw new InvalidDataException("The hub returned an incomplete or repeated worktree session or shell.");
      if(Blockers.Length > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, Blockers.Select(it => it.Message)));
      if(Sessions.Any(it => it.Activity is not ("idle" or "closed")) || ShellOnlyHosts.Any(it => it.Activity != "idle"))
         throw new InvalidOperationException("The worktree has a session or shell that is working, waiting for input, or whose activity could not be verified.");
   }

   public string DescribeRemoval()
   {
      var shells = ShellOnlyHosts.Length + Sessions.Count(it => it.HostId is not null);
      var counts = $"{Sessions.Length} {(Sessions.Length == 1 ? "session" : "sessions")}, {shells} {(shells == 1 ? "shell" : "shells")}.";
      return $"Remove {WorktreePath}?\n\n{counts} Idle sessions and shells will close. Archive keeps conversations until you restore them. Delete allows recovery for one week.";
   }
}

sealed record WorktreeSession(string AgentCli, string SessionId, string? HostId, string Activity);
sealed record WorktreeShell(string HostId, string Activity);
sealed record RemovalBlocker(string Message);
