namespace Cloyd.Projects.Git;

/// <summary>The linked checkout and revision that Magnus is being asked to remove.
/// Optional deletion of the local branch is bound to the reviewed commit.</summary>
sealed record WorktreeRemoval(Project Repository, GitWorktree Checkout, string? Commit, WorktreeStatus Status, BranchOnOrigin Remote, string? IntegrationBranch, string? StructuralBlocker)
{
   public bool CanDeleteBranch => Checkout.Branch is not null && Checkout.Branch != IntegrationBranch && Commit is not null and not "(initial)";

   public static async Task<WorktreeRemoval> Inspect(Project repository, Project folder, CancellationToken cancellation)
   {
      var reviewed = await ReadForReview(repository, folder, null, cancellation);
      if(reviewed.StructuralBlocker is {} blocker) throw new InvalidOperationException(blocker);
      return reviewed;
   }

   public static async Task<WorktreeRemoval> ReadForReview(Project repository, Project folder, Project? managedNested, CancellationToken cancellation)
   {
      var listing = await GitWorktrees.Read(repository, cancellation);
      var checkout = listing?.Checkouts.SingleOrDefault(it => it.Root.Equals(folder));
      if(listing is null || !listing.Repository.Equals(repository) || checkout is null)
         throw new InvalidOperationException($"{folder} is not a linked worktree of {repository}.");
      string? blocker = checkout.IsMain ? "The repository's main checkout cannot be removed."
         : checkout.Locked ? $"Git has locked {folder}."
         : listing.Checkouts.Any(it => !it.Root.Equals(folder) && folder.Contains(it.Root)) ? $"{folder} contains another registered worktree."
         : await Task.Run(() => NestedRepositoryBlocker(folder, managedNested, cancellation), cancellation);
      blocker ??= await ReviewedLocalBranch.OperationIn(folder, cancellation);
      var target = await GitIntegrationBranch.Of(repository, cancellation);
      var report = await GitStatusReport.Read(folder, cancellation);
      var status = target is not null && checkout.Branch == target.Name && report.Commit == target.Commit
         ? new WorktreeStatus(true, report.ChangedPaths, $"Merged into {target.Name}", CommitsOutsideIntegration: 0)
         : await WorktreeStatus.FromReport(checkout, target, report, cancellation);
      return new(repository, checkout, report.Commit, status, await BranchOnOrigin.Read(folder, report.Commit, checkout.Branch, cancellation), target?.Name, blocker);
   }

   // An ignored checkout may belong to a different repository. Removing its containing
   // worktree would delete that checkout even when the outer worktree is clean.
   static string? NestedRepositoryBlocker(Project folder, Project? managedNested, CancellationToken cancellation)
   {
      var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint };
      foreach(var directory in Directory.EnumerateDirectories(folder.Folder, "*", options))
      {
         cancellation.ThrowIfCancellationRequested();
         if(new Project(directory).Equals(managedNested)) continue;
         var nestedCheckout = Path.Exists(Path.Combine(directory, ".git"));
         var bareRepository = File.Exists(Path.Combine(directory, "HEAD")) && Directory.Exists(Path.Combine(directory, "objects"));
         if(nestedCheckout || bareRepository) return $"{folder} contains another Git checkout or bare repository at {directory}.";
      }
      return null;
   }

   public async Task Remove(CancellationToken cancellation, Func<Task>? requireVacant = null, bool deleteBranch = false)
   {
      if(deleteBranch && !CanDeleteBranch) throw new InvalidOperationException("The integration branch or a detached checkout cannot be deleted as a branch.");
      var now = await Inspect(Repository, Checkout.Root, cancellation);
      if(now != this || now.Status.ChangedPaths > 0)
         throw new InvalidOperationException($"The worktree changed. Review {Checkout.Root} again before removing it.");
      if(requireVacant is not null) await requireVacant();
      var last = await GitStatusReport.Read(Checkout.Root, cancellation);
      if(last.Commit != Commit || last.ChangedPaths != 0 || last.Branch != (Checkout.Branch ?? "(detached)"))
         throw new InvalidOperationException($"The checkout changed during the vacancy check. Review {Checkout.Root} again.");
      cancellation.ThrowIfCancellationRequested();
      (await GitCommand.In(Repository, CancellationToken.None, "worktree", "remove", "--", Checkout.Root.Folder)).CheckedOutput();
      if(deleteBranch) await new ReviewedLocalBranch(Repository, Checkout.Branch!, Commit!).Delete();
   }
}
