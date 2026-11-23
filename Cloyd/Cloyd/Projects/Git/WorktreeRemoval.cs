namespace Cloyd.Projects.Git;

/// <summary>The linked checkout and revision that Magnus is being asked to remove.
/// Git keeps the branch when it removes the checkout.</summary>
sealed record WorktreeRemoval(Project Repository, GitWorktree Checkout, string? Commit, WorktreeStatus Status)
{
   public static async Task<WorktreeRemoval> Inspect(Project repository, Project folder, CancellationToken cancellation)
   {
      var listing = await GitWorktrees.Read(repository, cancellation);
      var checkout = listing?.Checkouts.SingleOrDefault(it => it.Root.Equals(folder));
      if(listing is null || !listing.Repository.Equals(repository) || checkout is null || checkout.IsMain)
         throw new InvalidOperationException($"{folder} is not a linked worktree of {repository}.");
      if(checkout.Locked) throw new InvalidOperationException($"Git has locked {folder}.");
      if(listing.Checkouts.Any(it => !it.Root.Equals(folder) && folder.Contains(it.Root)))
         throw new InvalidOperationException($"{folder} contains another registered worktree.");
      await Task.Run(() => RequireNoNestedRepository(folder, cancellation), cancellation);
      var target = await GitIntegrationBranch.Of(repository, cancellation);
      var report = await GitStatusReport.Read(folder, cancellation);
      var status = await WorktreeStatus.FromReport(checkout, target, report, cancellation);
      return new(repository, checkout, report.Commit, status);
   }

   // An ignored checkout may belong to a different repository. Removing its containing
   // worktree would delete that checkout even when the outer worktree is clean.
   static void RequireNoNestedRepository(Project folder, CancellationToken cancellation)
   {
      var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint };
      var ownGitFile = Path.Combine(folder.Folder, ".git");
      foreach(var path in Directory.EnumerateFileSystemEntries(folder.Folder, ".git", options))
      {
         cancellation.ThrowIfCancellationRequested();
         if(!StringComparer.OrdinalIgnoreCase.Equals(path, ownGitFile))
            throw new InvalidOperationException($"{folder} contains another Git checkout at {Path.GetDirectoryName(path)}.");
      }
   }

   public async Task Remove(CancellationToken cancellation, Func<Task>? requireVacant = null)
   {
      var now = await Inspect(Repository, Checkout.Root, cancellation);
      if(now != this || now.Status.ChangedPaths > 0)
         throw new InvalidOperationException($"The worktree changed. Review {Checkout.Root} again before removing it.");
      if(requireVacant is not null) await requireVacant();
      cancellation.ThrowIfCancellationRequested();
      (await GitCommand.In(Repository, CancellationToken.None, "worktree", "remove", "--", Checkout.Root.Folder)).CheckedOutput();
   }
}
