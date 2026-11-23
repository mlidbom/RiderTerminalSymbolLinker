using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Hub.Worktrees;

namespace Cloyd.ProjectsDashboard;

sealed partial class WorktreeRemovalDialog
{
   readonly Func<SessionHostId, bool> _hasDraft;
   public event Action<SessionHostId>? ShellClosed;

   Control SessionCard(WorktreeRemovalReview review)
   {
      var rows = Stack(0);
      var live = review.Occupants.SelectMany(it => it.Rows).ToArray();
      if(live.Length > 0)
      {
         rows.Children.Add(new Border { Padding = new Thickness(12,10), Child = Notice("Live sessions and open tabs block removal",
            "Stop running agents and close their tabs before removing this worktree.", false) });
      }
      AddSessionFamilies(rows, review);
      if(live.Length > 0)
      {
         var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
         AddAction("Stop idle sessions", "stopAgent");
         AddAction("Close idle shell tabs", "closeShell");
         rows.Children.Add(new Border { Padding = new Thickness(12,8), Child = buttons });

         void AddAction(string label, string action)
         {
            var displayed = review.Occupants.Select(it => it with { Rows = it.Rows.Where(row => row.Action == action && Unavailable(row) is null).ToArray() }).ToArray();
            var count = displayed.Sum(it => it.Rows.Length);
            var button = ActionButton($"{label} ({count})", () => _ = Act(label + "…", () => ActOnOccupants(review.ReviewedHub, displayed, action)), _busy is null && count > 0);
            button.Margin = new Thickness(0,0,8,0);
            buttons.Children.Add(button);
         }
      }
      else if(!review.Subagents.Any)
         rows.Children.Add(new Border { Padding = new Thickness(15,11), Child = Label("No running sessions or open tabs.", 14, color: Muted) });
      var shownSubagentBlockers = review.Subagents.Blockers.Select(it => it.SessionId).ToHashSet();
      foreach(var blocker in review.Inventories.SelectMany(it => it.Blockers).Where(it => it.Code is not ("openHost" or "runningSession") &&
              !(it.Code == "activeDescendant" && it.SessionId is {} id && shownSubagentBlockers.Contains(id))).DistinctBy(it => it.Message))
         rows.Children.Add(Notice("Removal blocked", blocker.Message, false));
      return Card(rows);
   }

   string? Unavailable(WorktreeOccupant occupant)
   {
      if(occupant.Target.HostId is {} id && _hasDraft(new SessionHostId(id))) return "This tab has an unsent Cloyd draft.";
      if(_review?.Subagents.BlockingStopOf(occupant) is {} child) return child.Reason ?? $"{child.DisplayName} has outstanding work.";
      return occupant.Reason;
   }

   string NameOf(WorktreeOccupant occupant)
   {
      var target = occupant.Target;
      if(target.SessionId is null) return $"Shell · {target.HostId}";
      if(!string.IsNullOrWhiteSpace(occupant.Name)) return occupant.Name;
      if(target.AgentCli == "codex" && _review?.Subagents.ParentName(target.SessionId) is {} parentName) return parentName;
      return _nameOf(new WorktreeSession(target.AgentCli!, target.SessionId, target.HostId, occupant.Activity));
   }

   async Task<string?> ActOnOccupants(Cloyd.Hub.TheHub reviewedHub, WorktreeOccupants[] displayed, string action)
   {
      var outcomes = new List<string>();
      foreach(var group in displayed)
      {
         var eligible = group.Rows.Where(it => Unavailable(it) is null).ToArray();
         outcomes.AddRange(group.Rows.Except(eligible).Select(it => $"{NameOf(it)}: skipped. {Unavailable(it)}"));
         if(eligible.Length == 0) continue;
         var result = await reviewedHub.ActOnWorktreeOccupants(group.Root, action, eligible.Select(it => it.Target).ToArray(), _lifetime.Token);
         var stopped = result.Results.Where(it => it.Outcome == "stopped" && it.Target.HostId is not null).ToArray();
         foreach(var item in result.Results.Where(it => it.Outcome == "closed" && it.Target.HostId is not null))
            ShellClosed?.Invoke(new SessionHostId(item.Target.HostId!));
         if(stopped.Length > 0)
         {
            var hosts = (await reviewedHub.SessionHosts(_lifetime.Token)).Select(it => it.Id.Value).ToHashSet();
            foreach(var item in stopped.Where(it => !hosts.Contains(it.Target.HostId!) || it.Target.AgentPid == it.Target.ShellPid))
               ShellClosed?.Invoke(new SessionHostId(item.Target.HostId!));
         }
         outcomes.AddRange(result.Results.Select(it => $"{NameOf(eligible.Single(row => row.Target == it.Target))}: {it.Outcome}. {it.Reason}".TrimEnd()));
      }
      return outcomes.Count == 0 ? "No reviewed occupants are still eligible." : string.Join(Environment.NewLine, outcomes);
   }
}
