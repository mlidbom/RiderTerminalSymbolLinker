namespace Cloyd.Projects.Git;

/// <summary>The project worktree and its managed catalog worktree, reviewed together before either is changed.</summary>
sealed record WorktreeRemovalPlan(WorktreeRemoval ProjectCheckout, WorktreeRemoval? SharedRules)
{
   public IEnumerable<WorktreeRemoval> Checkouts => SharedRules is {} nested ? [nested, ProjectCheckout] : [ProjectCheckout];
   public bool CanRemove => Checkouts.All(it => it.StructuralBlocker is null && it.Status.ChangedPaths == 0);

   public static async Task<WorktreeRemovalPlan> Read(Project project, Project folder, CancellationToken cancellation, SharedRulesCatalog? catalog = null)
   {
      catalog ??= SharedRulesCatalog.OnThisMachine;
      var nested = await catalog.LinkedIn(folder, cancellation);
      var outer = WorktreeRemoval.ReadForReview(project, folder, nested, cancellation);
      if(nested is null) return new(await outer, null);
      var inner = WorktreeRemoval.ReadForReview(catalog.MachineClone, nested, null, cancellation);
      await Task.WhenAll(outer, inner);
      return new(await outer, await inner);
   }

   public async Task Remove(IReadOnlySet<Project> deleteBranches, Func<Task> requireVacant, CancellationToken cancellation)
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
            await nested.Remove(cancellation, requireVacant, deleteBranches.Contains(nested.Checkout.Root));
            nestedRemoved = true;
         }
         await ProjectCheckout.Remove(cancellation, requireVacant, deleteBranches.Contains(ProjectCheckout.Checkout.Root));
      }
      catch(Exception failure) when(nestedRemoved)
      {
         throw new InvalidOperationException($"The shared rules worktree was removed. {failure.Message}", failure);
      }
   }
}
