using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_a_project_worktree_uses_the_shared_rules : SpecificationOpeningWindows
{
   [XF] public async Task creation_attaches_the_catalog_and_removal_keeps_or_deletes_each_reviewed_branch_independently()
   {
      var main = Sandbox.Project("Project With Spaces").Project;
      var git = GitRepositoryOfASpecification.In(new DirectoryInfo(main.Folder));
      git.Track(".gitignore", "/.claude-shared/");
      git.Track(".claude/attach-shared-claude-config.sessionstart-hook.sh", "# registered consumer");
      Commit(git);
      git.Git("branch", "-M", "main");
      var machine = new Project(Path.Combine(Sandbox.ProjectsFolder, "private home", ".claude", ".claude-shared"));
      var rules = GitRepositoryOfASpecification.In(Directory.CreateDirectory(machine.Folder));
      var scripts = Environment.GetEnvironmentVariable("CLOYD_SPECIFICATIONS_CATALOG_SCRIPTS");
      if(scripts is null)
         rules.Track("git-scripts/sessionstart-hook.attach-the-catalog.sh",
            "set -eu\ngit -C \"$HOME/.claude/.claude-shared\" worktree add -b \"checkout/$(basename \"$1\" | tr ' ' '-')\" \"$1/.claude-shared\" main\n");
      else
         foreach(var file in new[] { "catalog-checkouts.sh", "sessionstart-hook.attach-the-catalog.sh", "sessionstart-hook.clean-up-after-deleted-checkouts.sh" })
            rules.Track("git-scripts/" + file, File.ReadAllText(Path.Combine(scripts, file)).Replace("\r\n", "\n"));
      Commit(rules);
      rules.Git("branch", "-M", "main");
      var remote = Path.Combine(Sandbox.ProjectsFolder, "catalog origin.git");
      rules.Git("init", "--bare", remote);
      rules.Git("remote", "add", "origin", remote);
      rules.Git("push", "-u", "origin", "main");
      var catalog = new SharedRulesCatalog(machine);

      foreach(var deleteProject in new[] { true, false })
      {
         var name = deleteProject ? "Review One" : "Review Two";
         var outer = await NewSessionWorktree.Create(main, name, default, catalog);
         var inner = SharedRulesCatalog.FolderIn(outer);
         (await catalog.LinkedIn(outer, default))!.Equals(inner).Must().BeTrue();
         var dirtyFile = Path.Combine(inner.Folder, "uncommitted.txt");
         File.WriteAllText(dirtyFile, "This belongs to the nested checkout");
         var dirty = await WorktreeRemovalPlan.Read(main, outer, default, catalog);
         dirty.ProjectCheckout.Status.ChangedPaths.Must().Be(0);
         dirty.SharedRules!.Status.ChangedPaths.Must().Be(1);
         await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => dirty.Remove(new HashSet<Project>(), () => Task.CompletedTask, default));
         Directory.Exists(outer.Folder).Must().BeTrue();
         File.Delete(dirtyFile);
         var plan = await WorktreeRemovalPlan.Read(main, outer, default, catalog);
         plan.SharedRules!.Remote.Verified.Must().BeTrue();
         await plan.Remove(new HashSet<Project> { deleteProject ? outer : inner }, () => Task.CompletedTask, default);
         Directory.Exists(outer.Folder).Must().BeFalse();
         if(scripts is not null)
         {
            var shell = (await GitCommand.In(machine, default, "var", "GIT_SHELL_PATH")).CheckedOutput().Trim();
            var cleanup = Path.Combine(machine.Folder, "git-scripts", "sessionstart-hook.clean-up-after-deleted-checkouts.sh").Replace('\\', '/');
            var start = new System.Diagnostics.ProcessStartInfo(shell, [cleanup]) { WorkingDirectory = machine.Folder };
            start.Environment["HOME"] = Directory.GetParent(machine.Folder)!.Parent!.FullName;
            start.Environment["PATH"] = Path.GetDirectoryName(shell) + Path.PathSeparator + start.Environment["PATH"];
            (await GitCommand.Run(start, default)).CheckedOutput();
         }
         (await HasBranch(main, plan.ProjectCheckout.Checkout.Branch!)).Must().Be(!deleteProject);
         (await HasBranch(machine, plan.SharedRules.Checkout.Branch!)).Must().Be(deleteProject);
         if(deleteProject) (await GitCommand.In(machine, default, "config", "--get", $"branch.{plan.SharedRules.Checkout.Branch}.cloydKeep")).CheckedOutput().Trim().Must().Be("true");
      }
   }

   [XF] public async Task an_ignored_bare_repository_blocks_removal_even_when_the_checkout_is_clean()
   {
      var root = Sandbox.Project("bare mirror").Project;
      var git = GitRepositoryOfASpecification.In(new DirectoryInfo(root.Folder));
      git.Track(".gitignore", "/backup.git/");
      var checkout = new Project(Path.Combine(Sandbox.ProjectsFolder, "contains mirror"));
      git.HasAWorktreeIn(new DirectoryInfo(checkout.Folder), "feature");
      var mirror = Path.Combine(checkout.Folder, "backup.git");
      git.Git("clone", "--bare", root.Folder, mirror);
      var review = await WorktreeRemovalPlan.Read(root, checkout, default);
      review.ProjectCheckout.Status.ChangedPaths.Must().Be(0);
      review.CanRemove.Must().BeFalse();
      await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => review.Remove(new HashSet<Project>(), () => Task.CompletedTask, default));
      File.Exists(Path.Combine(mirror, "HEAD")).Must().BeTrue();
   }

   [XF] public async Task a_verified_origin_copy_must_contain_the_current_tip_and_a_changed_tip_survives_removal()
   {
      var root = Sandbox.Project("origin checks").Project;
      var git = GitRepositoryOfASpecification.In(new DirectoryInfo(root.Folder));
      var checkout = new Project(Path.Combine(Sandbox.ProjectsFolder, "linked"));
      git.HasAWorktreeIn(new DirectoryInfo(checkout.Folder), "feature");
      var remote = Path.Combine(Sandbox.ProjectsFolder, "origin.git");
      git.Git("init", "--bare", remote);
      git.Git("remote", "add", "origin", remote);
      async Task CommitInCheckout() => (await GitCommand.In(checkout, default, "-c", "user.name=Spec", "-c", "user.email=spec@localhost", "-c", "commit.gpgsign=false",
         "commit", "--allow-empty", "-m", "Work outside main")).CheckedOutput();
      await CommitInCheckout();
      var unpushed = await WorktreeRemoval.Inspect(root, checkout, default);
      unpushed.Remote.Verified.Must().BeFalse();
      unpushed.Status.Integrated.Must().Be(false);
      git.Git("push", "origin", "feature:other-branch-name");
      var pushed = await WorktreeRemoval.Inspect(root, checkout, default);
      pushed.Remote.Description.Must().Be("origin/other-branch-name");
      pushed.Remote.Verified.Must().BeTrue();
      await CommitInCheckout();
      await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => pushed.Remove(default, deleteBranch: true));
      (await HasBranch(root, "feature")).Must().BeTrue();
      Directory.Exists(checkout.Folder).Must().BeTrue();
   }

   static async Task<bool> HasBranch(Project repository, string branch) =>
      (await GitCommand.In(repository, default, "show-ref", "--verify", "--quiet", "refs/heads/" + branch)).ExitCode == 0;
   static void Commit(GitRepositoryOfASpecification repository) => repository.Git("-c", "user.name=Spec", "-c", "user.email=spec@localhost",
      "-c", "commit.gpgsign=false", "commit", "--allow-empty", "-m", "Set up the private repository");
}
