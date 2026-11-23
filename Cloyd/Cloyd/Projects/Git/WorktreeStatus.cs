using System.Globalization;

namespace Cloyd.Projects.Git;

/// <summary>Whether the worktree's commits have been integrated, and how many paths have uncommitted
/// changes. A missing comparison leaves <see cref="Integrated"/> unknown while preserving the change count.</summary>
sealed record WorktreeStatus(bool? Integrated, int ChangedPaths, string Explanation, string Arrows = "", int? CommitsOutsideIntegration = null)
{
   public string Description => ChangedPaths == 0 ? Explanation : $"{Explanation} · {ChangedPaths} changed {(ChangedPaths == 1 ? "path" : "paths")}";

   public static WorktreeStatus Unavailable(Exception failure) => new(null, 0, $"Git status unavailable: {failure.Message}");

   public static async Task<WorktreeStatus> Read(GitWorktree checkout, GitIntegrationBranch? target, CancellationToken cancellation)
      => await FromReport(checkout, target, await GitStatusReport.Read(checkout.Root, cancellation), cancellation);

   public static async Task<WorktreeStatus> FromReport(GitWorktree checkout, GitIntegrationBranch? target, GitStatusReport status, CancellationToken cancellation)
   {
      if(status.Commit is null or "(initial)") return new(null, status.ChangedPaths, "No commits yet");
      if(target is null) return new(null, status.ChangedPaths, "No local integration branch");
      if(status.Branch == target.Name) return ComparedWithUpstream(status, target);

      var comparison = await GitCommand.In(checkout.Root, cancellation, "rev-list", "--count", $"{target.Commit}..{status.Commit}");
      if(comparison.ExitCode != 0) return new(null, status.ChangedPaths, $"Couldn't compare with {target.Name}: {comparison.Error.TrimEnd()}");
      var outside = int.Parse(comparison.Output, CultureInfo.InvariantCulture);
      return new(outside == 0, status.ChangedPaths,
         outside == 0 ? $"Merged into {target.Name}" : $"{outside} {(outside == 1 ? "commit" : "commits")} outside {target.Name}", CommitsOutsideIntegration: outside);
   }

   static WorktreeStatus ComparedWithUpstream(GitStatusReport status, GitIntegrationBranch target)
   {
      if(!target.TracksRemote || status.Upstream is not {} upstream) return new(null, status.ChangedPaths, $"{target.Name} has no remote upstream");
      if(status.AheadAndBehind is not {} counts) return new(null, status.ChangedPaths, $"No local tracking ref for {upstream}");
      return new(counts.Ahead == 0, status.ChangedPaths, $"{upstream} · {counts.Ahead} to push · {counts.Behind} to pull",
                 (counts.Ahead > 0 ? "↑" : "") + (counts.Behind > 0 ? "↓" : ""));
   }
}
