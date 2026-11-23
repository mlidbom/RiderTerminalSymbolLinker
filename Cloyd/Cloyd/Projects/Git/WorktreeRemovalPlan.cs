namespace Cloyd.Projects.Git;

/// <summary>The project worktree and its managed catalog worktree, reviewed together before either is changed.</summary>
sealed record WorktreeRemovalPlan(WorktreeRemoval ProjectCheckout, WorktreeRemoval? SharedRules, bool ExpectSharedRulesPathAbsent)
{
   public IEnumerable<WorktreeRemoval> Checkouts => SharedRules is {} nested ? [nested, ProjectCheckout] : [ProjectCheckout];
   public bool CanRemove => Checkouts.All(it => it.StructuralBlocker is null && it.Status.ChangedPaths == 0);

   public static async Task<WorktreeRemovalPlan> Read(Project project, Project folder, CancellationToken cancellation, SharedRulesCatalog? catalog = null)
   {
      catalog ??= SharedRulesCatalog.OnThisMachine;
      var nested = await catalog.LinkedIn(folder, cancellation);
      var outer = WorktreeRemoval.ReadForReview(project, folder, nested, cancellation);
      if(nested is null)
      {
         var checkout = await outer;
         var rulesFolder = SharedRulesCatalog.FolderIn(folder);
         if(checkout.StructuralBlocker is null && Path.Exists(rulesFolder.Folder) && await catalog.IsUsedBy(project, folder, cancellation))
            checkout = checkout with { StructuralBlocker = $"The path {rulesFolder} is occupied but is no longer a registered shared rules worktree. Review its contents first." };
         return new(checkout, null, !Path.Exists(rulesFolder.Folder));
      }
      var inner = WorktreeRemoval.ReadForReview(catalog.MachineClone, nested, null, cancellation);
      await Task.WhenAll(outer, inner);
      return new(await outer, await inner, true);
   }

   public async Task<string?> Remove(IReadOnlySet<Project> deleteBranches, Func<Task> requireVacant, CancellationToken cancellation,
                                     WorktreeDirectoryCleanups? directoryCleanups = null)
   {
      if(!CanRemove || deleteBranches.Any(folder => !Checkouts.Any(it => it.CanDeleteBranch && it.Checkout.Root.Equals(folder))))
         throw new InvalidOperationException("The requested worktrees and branches cannot be removed.");
      var nestedRemoved = false;
      try
      {
         if(SharedRules is {} nested)
         {
            if(!deleteBranches.Contains(nested.Checkout.Root) && nested.Checkout.Branch is {} keep)
               (await GitCommand.In(nested.Repository, cancellation, "config", "--local", $"branch.{keep}.cloydKeep", "true")).CheckedOutput();
            var deferred = await nested.Remove(cancellation, requireVacant, deleteBranches.Contains(nested.Checkout.Root), directoryCleanups);
            if(deferred is not null)
               return $"Shared rules worktree removed. Windows is holding the empty folder {deferred} open. Cleanup will retry automatically. " +
                      $"The project worktree at {ProjectCheckout.Checkout.Root} is still registered. Review it again after the folder is gone.";
            nestedRemoved = true;
         }
         var expectedAbsent = ExpectSharedRulesPathAbsent ? SharedRulesCatalog.FolderIn(ProjectCheckout.Checkout.Root) : null;
         var remaining = await ProjectCheckout.Remove(cancellation, requireVacant, deleteBranches.Contains(ProjectCheckout.Checkout.Root), directoryCleanups, expectedAbsent);
         if(remaining is null) return null;
         var deleted = Checkouts.Where(it => deleteBranches.Contains(it.Checkout.Root)).Select(it => it.Checkout.Branch).ToArray();
         var branches = deleted.Length == 0 ? "" : $" Deleted local {(deleted.Length == 1 ? "branch" : "branches")} {string.Join(", ", deleted)}.";
         return $"Worktree removed.{branches} Windows is holding the empty folder {remaining} open. Cleanup will retry automatically.";
      }
      catch(WorktreeRemovalNeedsAttention failure) when(nestedRemoved)
      {
         throw new WorktreeRemovalNeedsAttention($"The shared rules worktree was removed. {failure.Message}", failure);
      }
      catch(Exception failure) when(nestedRemoved)
      {
         throw new InvalidOperationException($"The shared rules worktree was removed. {failure.Message}", failure);
      }
   }
}
