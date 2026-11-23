using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cloyd.Projects.Git;

namespace Cloyd.ProjectsDashboard;

sealed partial class WorktreeRemovalDialog
{
   static readonly Geometry BranchIcon = Geometry.Parse("M5 2 A2 2 0 1 0 5 6 A2 2 0 1 0 5 2 Z M4 6 H6 V13 C7 11 13 12 13 8 V7 H15 V8 C15 14 7 13 6 17 V18 H4 Z M14 3 A2 2 0 1 0 14 7 A2 2 0 1 0 14 3 Z M5 17 A2 2 0 1 0 5 21 A2 2 0 1 0 5 17 Z");

   Control GitCard(WorktreeRemovalPlan plan)
   {
      var rows = Stack(0);
      rows.Children.Add(RepoRow(plan.ProjectCheckout, "Project"));
      if(plan.SharedRules is {} rules) { rows.Children.Add(Divider()); rows.Children.Add(RepoRow(rules, "Shared rules  ·  .claude-shared")); }
      return Card(rows);
   }

   static bool AtRisk(WorktreeRemoval checkout) => checkout.Status.Integrated != true && !checkout.Remote.Verified;
   bool DeletesCommitReference(WorktreeRemoval checkout) => checkout.Checkout.Branch is null || _deleteBranches.Contains(checkout.Checkout.Root);

   Control RepoRow(WorktreeRemoval repo, string heading)
   {
      var row = Stack(9);
      var head = Grid("*,Auto", 10);
      var names = Stack(2);
      names.Children.Add(Label(heading, 15, FontWeight.SemiBold));
      var branch = Grid("12,*", 6);
      Put(branch, new PathIcon { Data = BranchIcon, Width = 12, Height = 12, Foreground = Muted }, 0);
      Put(branch, Label(repo.Checkout.Branch ?? $"Detached at {repo.Commit}", 12, color: Muted), 1);
      names.Children.Add(branch);
      Put(head, names, 0);
      if(repo.Checkout.Branch is not null) Put(head, BranchChoice(repo), 1);
      row.Children.Add(head);
      var status = Grid("1*,1.12*,1.6*", 14);
      var changes = repo.Status.ChangedPaths;
      Put(status, Field("Working tree", changes == 0 ? "Clean" : $"{changes} changed paths", changes == 0 ? Green : Amber), 0);
      Put(status, Field("Integration" + (repo.IntegrationBranch is {} target ? $" into {target}" : ""), repo.Status.Explanation, repo.Status.Integrated == true ? Green : Amber), 1);
      Put(status, Field("Remote copy", repo.Remote.Description, repo.Remote.Verified ? Ink : Amber), 2);
      row.Children.Add(status);
      if(repo.StructuralBlocker is {} blocker) row.Children.Add(Notice("This worktree cannot be removed", blocker, false));
      if(changes > 0)
      {
         row.Children.Add(Notice("Changes must be committed or discarded", $"Open this checkout in Git Extensions to review its {changes} changed paths.", false));
         row.Children.Add(ActionButton("Open in Git Extensions", () =>
         {
            try { _showChanges(repo.Checkout.Root); }
            catch(Exception failure) { _message = failure.Message; RenderReview(); }
         }, !_mutating));
      }
      if(repo.Status.Integrated != true) row.Children.Add(BranchWarning(repo));
      return new Border { Padding = new Thickness(15,11), Child = row };
   }

   Control BranchWarning(WorktreeRemoval repo)
   {
      var detail = repo.Remote.Verified ? $"Its current tip is verified on {repo.Remote.Description}." : repo.Remote.Description + ".";
      if(!DeletesCommitReference(repo)) return Notice(repo.Status.Explanation, $"The local branch will remain. {detail}", false);
      if(repo.Remote.Verified) return Notice(repo.Status.Explanation, $"The current tip will remain on {repo.Remote.Description}.", false);
      var commits = repo.Status.CommitsOutsideIntegration is {} count ? $"{count} {(count == 1 ? "commit" : "commits")}" : "commits";
      var heading = repo.Checkout.Branch is null ? "Removing this detached worktree" : "Deleting this branch";
      return Notice($"{heading} may permanently lose {commits} of source code.", $"{repo.Status.Explanation}. {detail}", true);
   }

   Control BranchChoice(WorktreeRemoval repo)
   {
      var checkedOut = repo.Checkout.Root;
      var merged = repo.Status.Integrated == true;
      var risk = AtRisk(repo);
      var ink = merged ? Brush.Parse("#196438") : risk ? Brush.Parse("#AB1525") : Brush.Parse("#776000");
      var outline = merged ? Brush.Parse("#237747") : risk ? Brush.Parse("#B92233") : Brush.Parse("#AA8700");
      var delete = new CheckBox
      {
         Content = Label("Delete local branch", 13, FontWeight.Bold, ink), IsChecked = _deleteBranches.Contains(checkedOut),
         IsEnabled = repo.CanDeleteBranch && repo.StructuralBlocker is null && _busy is null,
         VerticalAlignment = VerticalAlignment.Center
      };
      Avalonia.Automation.AutomationProperties.SetName(delete, $"Delete local branch {repo.Checkout.Branch}");
      foreach(var state in new[] { "", "PointerOver", "Pressed" })
      {
         delete.Resources["CheckBoxCheckBackgroundStrokeUnchecked" + state] = outline;
         delete.Resources["CheckBoxCheckBackgroundStrokeChecked" + state] = outline;
         delete.Resources["CheckBoxCheckBackgroundFillUnchecked" + state] = Brushes.White;
         delete.Resources["CheckBoxCheckBackgroundFillChecked" + state] = merged || risk ? outline : Brush.Parse("#F3CE2C");
         delete.Resources["CheckBoxCheckGlyphForegroundChecked" + state] = merged || risk ? Brushes.White : Brush.Parse("#423400");
      }
      delete.IsCheckedChanged += (_, _) =>
      {
         if(delete.IsChecked == true) _deleteBranches.Add(checkedOut);
         else _deleteBranches.Remove(checkedOut);
         RenderReview();
      };
      return new Border { Background = Brush.Parse(merged ? "#E9F6EB" : risk ? "#FDEBEF" : "#FFF4B5"), CornerRadius = new CornerRadius(4),
         Padding = new Thickness(7,0), VerticalAlignment = VerticalAlignment.Top, Child = delete };
   }
}
