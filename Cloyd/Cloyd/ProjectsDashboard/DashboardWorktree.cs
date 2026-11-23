using Cloyd.Projects;
using Cloyd.Projects.Git;
using Avalonia.Media;

namespace Cloyd.ProjectsDashboard;

/// <summary>A Git worktree, shown even while it has no sessions.</summary>
sealed class DashboardWorktree(GitWorktree checkout) : DashboardGroup(KeyFor(checkout.Root))
{
   GitWorktree _checkout = checkout;
   public static string KeyFor(Project root) => $"worktree:{root.Folder}";
   public override string Name => _checkout.Name;
   public string? Branch => _checkout.Branch;
   public bool CanRemove => RemovalBlocker is null;
   public string? RemovalBlocker { get => field; private set => SetProperty(ref field, value); } = "Checking this worktree.";

   public void UpdateRemovalAvailability()
   {
      if(_checkout.IsMain) RemovalBlocker = "This is the repository's main checkout.";
      else if(_checkout.Locked) RemovalBlocker = "Git has locked this worktree.";
      else
      {
         var open = Sessions.Where(it => it.HasHost || it.Running is not null).Select(it => it.Name).ToList();
         RemovalBlocker = open.Count == 0 ? null : $"Close the open tabs or running sessions in this worktree ({string.Join(", ", open)}).";
      }
   }
   public override string Description => Status is {} status ? $"{_checkout.Description} · {status.Description}" : _checkout.Description;
   public override Project StartFolder => _checkout.Root;
   public WorktreeStatus? Status
   {
      get => field;
      set
      {
         if(!SetProperty(ref field, value)) return;
         OnPropertyChanged(nameof(Description));
         OnPropertyChanged(nameof(BranchColour));
         OnPropertyChanged(nameof(HasUncommittedChanges));
         OnPropertyChanged(nameof(PushPullArrows));
      }
   }
   public IBrush BranchColour => Status?.Integrated switch { true => Brushes.SeaGreen, false => Brushes.MediumPurple, null => Brushes.Gray };
   public bool HasUncommittedChanges => Status?.ChangedPaths > 0;
   public string PushPullArrows => Status?.Arrows ?? "";

   /// <summary>A branch switch invalidates the last reading even though the checkout's folder is the same.</summary>
   public void ShowGitStatus(DashboardGitStatuses statuses) => Status = statuses.Of(_checkout);

   public void Update(GitWorktree checkout)
   {
      if(_checkout == checkout) return;
      _checkout = checkout;
      OnPropertyChanged(nameof(Name));
      OnPropertyChanged(nameof(Description));
   }
}
