using Cloyd.Hub.Worktrees;
using Cloyd.Projects;

namespace Cloyd.ProjectsDashboard;

/// <summary>Codex helpers and their parents across all checkouts in the review.</summary>
sealed class WorktreeSubagents
{
   readonly Dictionary<string, WorktreeSubagent> _byId;
   readonly ILookup<string, WorktreeSubagent> _byParent;
   readonly Dictionary<string, Project> _worktrees = [];
   readonly Dictionary<string, WorktreeSubagent> _blockingStopOf = [];

   public WorktreeSubagents(WorktreeOccupants[] checkouts)
   {
      // A blocker in either reading stays visible until the next review confirms it has cleared.
      _byId = [];
      foreach(var checkout in checkouts)
      foreach(var child in checkout.Descendants)
      {
         if(_byId.TryGetValue(child.SessionId, out var known) && (known.BlocksRemoval || !child.BlocksRemoval)) continue;
         _byId[child.SessionId] = child;
         _worktrees[child.SessionId] = checkout.Root;
      }
      foreach(var occupant in checkouts.SelectMany(it => it.Rows))
      {
         if(!Includes(occupant.Target) || occupant.Target.HostId is null && occupant.Target.AgentPid is null) continue;
         var child = _byId[occupant.Target.SessionId!];
         if(!child.BlocksRemoval)
            _byId[child.SessionId] = child with { BlocksRemoval = true, Activity = "unknown",
               Reason = $"{child.DisplayName} still has a reported terminal or process. Refresh after it exits." };
      }
      _byParent = _byId.Values.ToLookup(it => it.ParentSessionId);
      foreach(var child in _byId.Values)
      foreach(var ancestor in AncestorsOf(child))
         if(child.BlocksRemoval) _blockingStopOf.TryAdd(ancestor, child);
   }

   public bool Any => _byId.Count > 0;
   public IEnumerable<WorktreeSubagent> Blockers => _byId.Values.Where(it => it.BlocksRemoval);
   public bool Includes(OccupantTarget target) => target is { AgentCli: "codex", SessionId: {} id } && _byId.ContainsKey(id);
   public WorktreeOccupants Reconcile(WorktreeOccupants checkout) => checkout with
   {
      Rows = checkout.Rows.Where(it => !Includes(it.Target) || it.Target.HostId is not null || it.Target.AgentPid is not null)
         .Select(it => Includes(it.Target) ? it with { Action = null, Reason = "Codex also reports this helper in a live terminal or process. Refresh after it exits." } : it).ToArray()
   };

   public void RequireSeparateArchiveTargets(WorktreeSessionsPreview inventory)
   {
      foreach(var session in inventory.Sessions)
         if(session.AgentCli == "codex" && _byId.TryGetValue(session.SessionId, out var child))
            throw new InvalidDataException($"Codex reported {child.DisplayName} as both a helper and a separate session. Refresh the review before continuing.");
   }
   public IEnumerable<WorktreeSubagent> ChildrenOf(string parent) => _byParent[parent];
   public Project WorktreeOf(WorktreeSubagent child) => _worktrees[child.SessionId];
   public string? ParentName(string parent) => ChildrenOf(parent).Select(it => it.ParentName).FirstOrDefault(it => !string.IsNullOrWhiteSpace(it));
   public IEnumerable<IGrouping<string, WorktreeSubagent>> RootFamilies => _byParent.Where(it => !_byId.ContainsKey(it.Key));

   public WorktreeSubagent? BlockingStopOf(WorktreeOccupant occupant) =>
      occupant is { Action: "stopAgent", Target: { AgentCli: "codex", SessionId: {} session } } ? _blockingStopOf.GetValueOrDefault(session) : null;

   IEnumerable<string> AncestorsOf(WorktreeSubagent child)
   {
      var seen = new HashSet<string> { child.SessionId };
      var id = child.ParentSessionId;
      while(seen.Add(id))
      {
         yield return id;
         if(!_byId.TryGetValue(id, out var parent)) yield break;
         id = parent.ParentSessionId;
      }
      throw new InvalidDataException($"The hub reported a cycle in the parent sessions of {child.DisplayName}.");
   }
}
