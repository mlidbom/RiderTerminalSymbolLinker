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
         foreach(var group in review.Occupants)
         foreach(var occupant in group.Rows)
         {
            var grid = Grid("1.25*,1*,1*", 10);
            Put(grid, Label(NameOf(occupant), 14, FontWeight.SemiBold), 0);
            Put(grid, Label(occupant.Activity switch { "idle" when occupant.Target.SessionId is null => "Idle shell", "waitingForInput" => "Waiting for you",
               "working" => "Working", "idle" => "Idle", _ => "Activity unknown" }, 13), 1);
            Put(grid, Label(occupant.Target.HostId is null ? "Outside Cloyd" : "Open tab", 13, color: Muted), 2);
            var details = Stack(2);
            details.Children.Add(grid);
            if(review.Git.SharedRules?.Checkout.Root.Equals(group.Root) == true) details.Children.Add(Label("Shared rules · .claude-shared", 12, color: Muted));
            if(Unavailable(occupant) is {} reason) details.Children.Add(Label(reason, 12, color: Muted));
            rows.Children.Add(new Border { Padding = new Thickness(15,7), Child = details });
            rows.Children.Add(Divider());
         }
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
      else rows.Children.Add(new Border { Padding = new Thickness(15,11), Child = Label("No running sessions or open tabs.", 14, color: Muted) });
      foreach(var blocker in review.Inventories.SelectMany(it => it.Blockers).Where(it => it.Code is not ("openHost" or "runningSession")).DistinctBy(it => it.Message))
         rows.Children.Add(Notice("Removal blocked", blocker.Message, false));
      return Card(rows);
   }

   string? Unavailable(WorktreeOccupant occupant) => occupant.Target.HostId is {} id && _hasDraft(new SessionHostId(id))
      ? "This tab has an unsent Cloyd draft." : occupant.Reason;

   string NameOf(WorktreeOccupant occupant)
   {
      var target = occupant.Target;
      if(target.SessionId is null) return $"Shell · {target.HostId}";
      return !string.IsNullOrWhiteSpace(occupant.Name) ? occupant.Name :
         _nameOf(new WorktreeSession(target.AgentCli!, target.SessionId, target.HostId, occupant.Activity));
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
