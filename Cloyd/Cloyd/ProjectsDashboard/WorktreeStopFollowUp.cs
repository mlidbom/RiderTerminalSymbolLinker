using Cloyd.Hub.Worktrees;
using Cloyd.Projects;

namespace Cloyd.ProjectsDashboard;

/// <summary>Follows a confirmed terminal exit until the session unloads and its original shell
/// returns. Codex normally unloads an unsubscribed session after one minute.</summary>
sealed class WorktreeStopFollowUp(TimeProvider clock)
{
   static readonly TimeSpan WatchFor = TimeSpan.FromSeconds(75);
   readonly Dictionary<OccupantTarget, Exit> _stopped = [];
   readonly HashSet<OccupantTarget> _confirmed = [];

   sealed record Exit(Project Worktree, long At, bool AwaitingShell);

   public void Stopped(Project worktree, OccupantTarget agent)
   {
      _confirmed.Add(agent);
      _stopped[agent] = new Exit(worktree, clock.GetTimestamp(), agent.HostId is not null && agent.ShellPid != agent.AgentPid);
   }

   public void ShellClosed(string host)
   {
      foreach(var (target, exit) in _stopped.Where(it => it.Key.HostId == host).ToArray())
         _stopped[target] = exit with { AwaitingShell = false };
   }

   public TimeSpan UntilNextExpiry => _stopped.Values.Min(Remaining);
   TimeSpan Remaining(Exit exit) => WatchFor - clock.GetElapsedTime(exit.At);
   public void Finish() => _stopped.Clear();
   public bool ExitConfirmed(OccupantTarget target) => _confirmed.Contains(target);

   public bool IsUnloading(WorktreeOccupant row) => _stopped.ContainsKey(row.Target) ||
      (row is { Activity: "unknown", Target: { AgentCli: "codex", HostId: null, AgentPid: null } } &&
       _stopped.Keys.Any(target => SameSession(target, row.Target)));

   public bool IsWaitingForPrompt(WorktreeOccupant row) =>
      row is { Activity: "unknown", Action: null, Target: { SessionId: null, HostId: {} shell } } &&
      _stopped.Any(it => it.Key.HostId == shell && it.Value.AwaitingShell);

   public bool StillWaiting(WorktreeOccupants[] checkouts)
   {
      var rows = checkouts.SelectMany(it => it.Rows).ToArray();
      foreach(var (target, exit) in _stopped.ToArray())
      {
         var originalHost = rows.Where(it => it.Target.HostId == target.HostId);
         var elsewhere = checkouts.Any(it => it.Root.Equals(exit.Worktree) && it.VerifiedShellsElsewhere.Contains(target.HostId));
         var awaitingShell = exit.AwaitingShell && !elsewhere && !originalHost.Any(it => it.Action == "closeShell" ||
            it.Target.AgentPid is not null && it.Target != target);
         var awaitingAgent = rows.Any(it => SameSession(target, it.Target) && (it.Target.AgentPid is null || it.Target == target));
         if(Remaining(exit) <= TimeSpan.Zero || !(awaitingAgent || awaitingShell)) _stopped.Remove(target);
         else if(!awaitingShell && exit.AwaitingShell) _stopped[target] = exit with { AwaitingShell = false };
      }
      return _stopped.Count > 0;
   }

   public sealed record Observation(HashSet<WorktreeOccupant> Rows, HashSet<OccupantTarget> ShellsElsewhere)
   {
      public bool Matches(Observation other) => Rows.SetEquals(other.Rows) && ShellsElsewhere.SetEquals(other.ShellsElsewhere);
   }

   public Observation Observe(WorktreeOccupants[] checkouts)
   {
      var rows = checkouts.SelectMany(it => it.Rows).Where(row =>
         _stopped.Keys.Any(target => SameSession(target, row.Target) || target.HostId == row.Target.HostId)).ToHashSet();
      var elsewhere = _stopped.Where(entry => checkouts.Any(it => it.Root.Equals(entry.Value.Worktree) &&
         it.VerifiedShellsElsewhere.Contains(entry.Key.HostId))).Select(it => it.Key).ToHashSet();
      return new(rows, elsewhere);
   }

   /// <summary>Poll the live occupants. A complete preview records an archive operation
   /// in the hub journal and is needed only when the presented state changes.</summary>
   public async Task<Observation> ReadOccupants(WorktreeRemovalReview review, CancellationToken cancellation)
   {
      var readings = await Task.WhenAll(_stopped.Values.Select(it => it.Worktree).Distinct().Select(async folder =>
      {
         var reading = await review.ReviewedHub.ReviewWorktreeOccupants(folder, cancellation);
         return new WorktreeOccupants(folder, reading?.Occupants ?? [], []) { VerifiedShellsElsewhere = reading?.VerifiedShellsElsewhere ?? [] };
      }));
      return Observe(readings);
   }

   static bool SameSession(OccupantTarget stopped, OccupantTarget current) =>
      stopped.AgentCli == current.AgentCli && stopped.SessionId == current.SessionId;
}
