using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Cloyd.Hub.Worktrees;

namespace Cloyd.ProjectsDashboard;

sealed partial class WorktreeRemovalDialog
{
   /// <summary>Subagents appear beneath the session that spawned them. A parent without a tab
   /// still has a heading when its helpers are part of this checkout's review.</summary>
   void AddSessionFamilies(StackPanel rows, WorktreeRemovalReview review)
   {
      var children = review.Subagents;
      var shown = new HashSet<string>();
      var catalog = review.Git.SharedRules?.Checkout.Root;
      const string catalogLabel = "Shared rules · .claude-shared";
      var helperProcesses = review.Occupants.SelectMany(it => it.Rows).Where(it => children.Includes(it.Target)).ToLookup(it => it.Target.SessionId);
      foreach(var group in review.Occupants)
      foreach(var occupant in group.Rows.Where(it => !children.Includes(it.Target)))
      {
         var activity = occupant is { Activity: "idle", Target.SessionId: null } ? "Idle shell" : ActivityText(occupant.Activity);
         var parent = occupant.Target.AgentCli == "codex" ? occupant.Target.SessionId : null;
         var unloading = _afterStop.IsUnloading(occupant);
         rows.Children.Add(SessionRow(NameOf(occupant), unloading ? "Terminal stopped" : _afterStop.IsWaitingForPrompt(occupant) ? "Rechecking shell" : activity,
                                     unloading ? "Rechecking session" : occupant.Target.HostId is null ? "Outside Cloyd" : "Open tab", 0,
                                     group.Root.Equals(catalog) ? catalogLabel : null, unloading ? "The session is still listed. Checking for an update." : Unavailable(occupant)));
         if(parent is not null) AddChildren(parent, 1);
         rows.Children.Add(Divider());
      }

      foreach(var family in children.RootFamilies.Where(it => it.Any(child => !shown.Contains(child.SessionId))))
      {
         var parentName = children.ParentName(family.Key)
                          ?? _nameOf(new WorktreeSession("codex", family.Key, null, "closed"));
         rows.Children.Add(SessionRow(parentName, "", "Parent session", 0));
         AddChildren(family.Key, 1);
         rows.Children.Add(Divider());
      }

      void AddChildren(string parent, int depth)
      {
         foreach(var child in children.ChildrenOf(parent))
         {
            if(!shown.Add(child.SessionId)) continue;
            var process = helperProcesses[child.SessionId].FirstOrDefault();
            var blocks = child.BlocksRemoval || process is not null;
            var reason = child.Reason ?? process?.Reason ?? (blocks ? "This helper must finish before removal." : null);
            var row = SessionRow(child.DisplayName, ActivityText(child.Activity), blocks ? "Subagent · blocks removal" : "Subagent", depth,
                                 children.WorktreeOf(child).Equals(catalog) ? catalogLabel : null, reason);
            ToolTip.SetTip(row, child.AgentPath is {} path ? $"{path} · {child.SessionId}" : child.SessionId);
            rows.Children.Add(row);
            AddChildren(child.SessionId, depth + 1);
         }
      }
   }

   static string ActivityText(string activity) => activity switch
   {
      "working" => "Working", "waitingForInput" => "Waiting for you", "idle" => "Idle", "completed" => "Completed",
      "queued" => "Queued work", "background" => "Background work", "unknown" => "Activity unknown", _ => activity
   };

   Border SessionRow(string name, string activity, string location, int depth, params string?[] notes)
   {
      var grid = Grid("1.25*,1*,1*", 10);
      Put(grid, Label(name, 14, FontWeight.SemiBold), 0);
      Put(grid, Label(activity, 13), 1);
      Put(grid, Label(location, 13, color: Muted), 2);
      var details = Stack(2);
      details.Children.Add(grid);
      foreach(var note in notes.OfType<string>()) details.Children.Add(Label(note, 12, color: Muted));
      return new Border { Padding = new Thickness(15 + 16 * Math.Min(depth, 6), 7, 15, 7), Child = details };
   }
}
