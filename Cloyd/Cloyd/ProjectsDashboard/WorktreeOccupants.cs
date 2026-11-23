using Cloyd.Hub.Worktrees;
using Cloyd.Projects;

namespace Cloyd.ProjectsDashboard;

/// <summary>The live occupants of one of the reviewed checkouts, including any newly discovered by the hub's action review.</summary>
sealed record WorktreeOccupants(Project Root, WorktreeOccupant[] Rows)
{
   public static WorktreeOccupants From(WorktreeSessionsPreview inventory, ReviewedOccupants? eligibility, IEnumerable<WorktreeOccupant>? dashboard = null)
   {
      var rows = eligibility?.Occupants.ToList() ?? [];
      foreach(var session in inventory.Sessions.Where(it => it.IsOpen))
         if(!rows.Any(it => it.Target.SessionId == session.SessionId && it.Target.AgentCli == session.AgentCli && it.Target.HostId == session.HostId))
            rows.Add(new(new(session.HostId, null, session.AgentCli, session.SessionId, null, null), session.Activity, null, Reason()));
      foreach(var shell in inventory.ShellOnlyHosts)
         if(!rows.Any(it => it.Target.HostId == shell.HostId))
            rows.Add(new(new(shell.HostId, null, null, null, null, null), "unknown", null, Reason()));
      foreach(var local in dashboard ?? [])
         if(!rows.Any(it => local.Target.HostId is {} host ? it.Target.HostId == host :
               it.Target.SessionId == local.Target.SessionId && it.Target.AgentCli == local.Target.AgentCli))
            rows.Add(local);
      return new(new Project(inventory.WorktreePath), rows.ToArray());
      string Reason() => eligibility is null ? "This hub cannot verify idle occupants. Stop or close it manually." : "Occupant changed during review. Refresh to check again.";
   }
}
