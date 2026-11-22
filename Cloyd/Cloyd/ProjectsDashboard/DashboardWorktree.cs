using Cloyd.Projects;
using Cloyd.Projects.Git;

namespace Cloyd.ProjectsDashboard;

/// <summary>A Git worktree, shown even while it has no sessions.</summary>
sealed class DashboardWorktree(GitWorktree checkout) : DashboardGroup(KeyFor(checkout.Root))
{
   GitWorktree _checkout = checkout;
   public static string KeyFor(Project root) => $"worktree:{root.Folder}";
   public override string Name => _checkout.Name;
   public override string Description => _checkout.Description;
   public override Project StartFolder => _checkout.Root;

   public void Update(GitWorktree checkout)
   {
      if(_checkout == checkout) return;
      _checkout = checkout;
      OnPropertyChanged(nameof(Name));
      OnPropertyChanged(nameof(Description));
   }
}
