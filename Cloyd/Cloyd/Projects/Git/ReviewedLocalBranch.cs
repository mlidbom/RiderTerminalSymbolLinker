namespace Cloyd.Projects.Git;

/// <summary>A branch may be removed only at its reviewed tip, once no worktree or Git operation owns it.</summary>
sealed record ReviewedLocalBranch(Project Repository, string Name, string Commit)
{
   public async Task Delete()
   {
      var cancellation = CancellationToken.None;
      var branchRef = "refs/heads/" + Name;
      var listing = await GitWorktrees.Read(Repository, cancellation);
      if(listing is null || listing.Checkouts.Any(it => it.Branch == Name))
         throw new InvalidOperationException($"Checkout removed. Kept {Name} because another worktree may be using it.");
      foreach(var checkout in listing.Checkouts)
      {
         var administration = (await GitCommand.In(checkout.Root, cancellation, "rev-parse", "--absolute-git-dir")).CheckedOutput().Trim();
         if(UsedByOperation(administration, branchRef))
            throw new InvalidOperationException($"Checkout removed. Kept {Name} because a Git operation in {checkout.Root} is using it.");
      }

      var removed = await GitCommand.In(Repository, cancellation, "update-ref", "--no-deref", "-d", branchRef, Commit);
      if(removed.ExitCode != 0) throw new InvalidOperationException($"Checkout removed. Could not delete the reviewed branch {Name}. {removed.Error.Trim()}");
      var existsAgain = await GitCommand.In(Repository, cancellation, "show-ref", "--verify", "--quiet", branchRef);
      if(existsAgain.ExitCode != 1) return;
      var config = await GitCommand.In(Repository, cancellation, "config", "--local", "--remove-section", "branch." + Name);
      if(config.ExitCode != 0 && !config.Error.Contains("no such section", StringComparison.Ordinal))
         throw new InvalidOperationException($"Removed {Name}, but could not clear its local branch configuration. {config.Error.Trim()}");
   }

   bool UsedByOperation(string administration, string branchRef)
   {
      foreach(var relative in new[] { "rebase-merge/head-name", "rebase-apply/head-name", "BISECT_START", "rebase-merge/update-refs", "sequencer/update-refs" })
      {
         var path = Path.Combine(administration, relative);
         if(File.Exists(path) && File.ReadLines(path).Any(it => it == branchRef || it == Name)) return true;
      }
      return false;
   }

   public static async Task<string?> OperationIn(Project worktree, CancellationToken cancellation)
   {
      var administration = (await GitCommand.In(worktree, cancellation, "rev-parse", "--absolute-git-dir")).CheckedOutput().Trim();
      foreach(var marker in new[] { "rebase-merge", "rebase-apply", "MERGE_HEAD", "BISECT_START", "CHERRY_PICK_HEAD", "REVERT_HEAD", "sequencer" })
         if(Path.Exists(Path.Combine(administration, marker))) return $"Finish or abort the Git operation in {worktree} before removing it ({marker}).";
      return null;
   }
}
