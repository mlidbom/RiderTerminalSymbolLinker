using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Cloyd.ClaudeCode;
using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.ProjectsDashboard;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_a_locked_worktree_directory_is_removed : SpecificationOpeningWindows
{
   [XF] public async Task Git_unregisters_the_locked_checkout_the_branch_is_deleted_and_the_dialog_shows_the_hubs_pending_cleanup()
   {
      var project = Sandbox.Project("locked project").Project;
      var folder = new Project(Path.Combine(Sandbox.ProjectsFolder, "locked directory"));
      var git = GitRepositoryOfASpecification.In(new DirectoryInfo(project.Folder));
      git.HasAWorktreeIn(new DirectoryInfo(folder.Folder), "retire");
      var sandbox = Sandbox.HubOfTheSandbox;
      if(!HasCleanup()) return;
      var recorded = new SessionOfASpecification(new ClaudeCodeSessionId(Guid.NewGuid().ToString()), folder.Folder, "A conversation to archive once.");
      recorded.WriteItsTranscriptFor(sandbox);
      var service = new RemoveDashboardWorktree(sandbox.Hub, Sandbox.CloydsFolder, project, folder);
      using var holder = HoldCurrentDirectory(folder);
      try
      {
         var dialog = Open(() => new WorktreeRemovalDialog(service, it => it.SessionId, _ => {}, _ => false, default));
         WaitUntil(() => Button("Remove worktree")?.IsEnabled == true, "the worktree could be removed");
         OnTheUIThread(() => dialog.GetVisualDescendants().OfType<CheckBox>()
            .Single(it => AutomationProperties.GetName(it) == "Delete local branch retire").IsChecked = true);
         Click(OnTheUIThread(() => Button("Remove and delete 1 local branch")!));
         WaitUntil(() => Button("Done") is not null, "the dialog reported deferred folder deletion", timeout: TimeSpan.FromSeconds(90));
         OnTheUIThread(() => dialog.GetVisualDescendants().OfType<TextBlock>().Any(it => it.Text?.Contains("Cleanup will retry automatically.") == true)).Must().BeTrue();
         OnTheUIThread(() => Button("Refresh") is null).Must().BeTrue();
         (await GitWorktrees.Read(project, default))!.RegisteredRoots.Contains(folder).Must().BeFalse();
         (await GitCommand.In(project, default, "show-ref", "--verify", "--quiet", "refs/heads/retire")).ExitCode.Must().Be(1);
         Directory.EnumerateFileSystemEntries(folder.Folder).Must().BeEmpty();
         (await sandbox.Hub.OutstandingDirectoryCleanup(folder, default))!.Status.Must().Be("pending");
         sandbox.SessionsOnDisk().Any(it => it.Session.Id == recorded.Id).Must().BeFalse();
         var archive = Path.Combine(sandbox.Environment["CLAUDE_CONFIG_DIR"], "session-archive");
         Directory.EnumerateFiles(archive, "archived-session.json", SearchOption.AllDirectories)
                  .Count(it => File.ReadAllText(it).Contains(recorded.Id.Value)).Must().Be(1);
         Click(OnTheUIThread(() => Button("Done")!));
         Button? Button(string label) => dialog.GetVisualDescendants().OfType<Button>().SingleOrDefault(it => Equals(it.Content, label));
      }
      finally { await Release(holder); }
      if(Environment.GetEnvironmentVariable("CLOYD_VERIFY_DIRECTORY_CLEANUP_TIMER") == "1")
      {
         using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
         while(Directory.Exists(folder.Folder)) await Task.Delay(TimeSpan.FromSeconds(1), deadline.Token);
         (await sandbox.Hub.OutstandingDirectoryCleanup(folder, deadline.Token) is null).Must().BeTrue();
      }
   }

   [XF] public async Task an_interrupted_handoff_can_replay_a_lost_receipt_without_repeating_Git_or_an_archive()
   {
      if(!HasCleanup()) return;
      var repository = Sandbox.Project("interrupted delivery").Project;
      var root = new Project(Path.Combine(Sandbox.ProjectsFolder, "orphan"));
      GitRepositoryOfASpecification.In(new DirectoryInfo(repository.Folder)).HasAWorktreeIn(new DirectoryInfo(root.Folder), "keep-me");
      var hub = Sandbox.HubOfTheSandbox.Hub;
      var original = (await hub.PrepareDirectoryCleanup(root, default))!;
      var handoff = Path.Combine(Sandbox.CloydsFolder.Folder.FullName, "directory-cleanup-handoffs", $"{original.CleanupId:N}.json");
      new FileWrittenWhole(handoff).Write(JsonSerializer.Serialize(new { original.CleanupId, WorktreePath = root.Folder, RepositoryPath = repository.Folder }));
      using var holder = HoldCurrentDirectory(root);
      try
      {
         (await GitCommand.In(repository, default, "worktree", "remove", "--", root.Folder)).ExitCode.Must().NotBe(0);
         (await hub.EnqueueDirectoryCleanup(root, original.CleanupId, default)).Status.Must().Be("pending");
         var restarted = new WorktreeDirectoryCleanups(hub, Sandbox.CloydsFolder);
         await restarted.Recover(default, failure => throw failure);
         File.Exists(handoff).Must().BeFalse();
         (await hub.OutstandingDirectoryCleanups(default)).Single().CleanupId.Must().Be(original.CleanupId);
         (await GitCommand.In(repository, default, "show-ref", "--verify", "--quiet", "refs/heads/keep-me")).ExitCode.Must().Be(0);
      }
      finally { await Release(holder); }
   }

   [XF] public async Task a_replacement_directory_is_left_alone_and_flagged_after_restarting_Cloyd()
   {
      if(!HasCleanup()) return;
      var repository = Sandbox.Project("replacement").Project;
      var root = new Project(Path.Combine(Sandbox.ProjectsFolder, "same folder name"));
      GitRepositoryOfASpecification.In(new DirectoryInfo(repository.Folder)).HasAWorktreeIn(new DirectoryInfo(root.Folder), "keep-me");
      var hub = Sandbox.HubOfTheSandbox.Hub;
      var original = (await hub.PrepareDirectoryCleanup(root, default))!;
      var handoff = Path.Combine(Sandbox.CloydsFolder.Folder.FullName, "directory-cleanup-handoffs", $"{original.CleanupId:N}.json");
      new FileWrittenWhole(handoff).Write(JsonSerializer.Serialize(new { original.CleanupId, WorktreePath = root.Folder, RepositoryPath = repository.Folder }));
      (await GitCommand.In(repository, default, "worktree", "remove", "--", root.Folder)).CheckedOutput();
      Directory.CreateDirectory(root.Folder);
      var problems = new List<Exception>();
      await new WorktreeDirectoryCleanups(hub, Sandbox.CloydsFolder).Recover(default, problems.Add);
      problems.Count.Must().Be(1);
      Directory.Exists(root.Folder).Must().BeTrue();
      (await hub.OutstandingDirectoryCleanup(root, default))!.Status.Must().Be("needsReview");
      File.Exists(handoff).Must().BeFalse();
   }

   [XF] public async Task recovery_flags_a_re_registered_checkout_and_waits_for_the_original_Git_process_to_exit()
   {
      if(!HasCleanup()) return;
      var repository = Sandbox.Project("re-registration").Project;
      var root = new Project(Path.Combine(Sandbox.ProjectsFolder, "reused"));
      GitRepositoryOfASpecification.In(new DirectoryInfo(repository.Folder)).HasAWorktreeIn(new DirectoryInfo(root.Folder), "original");
      var hub = Sandbox.HubOfTheSandbox.Hub;
      var receipt = (await hub.PrepareDirectoryCleanup(root, default))!;
      (await GitCommand.In(repository, default, "worktree", "remove", "--", root.Folder)).CheckedOutput();
      (await GitCommand.In(repository, default, "worktree", "add", "-b", "replacement", "--", root.Folder)).CheckedOutput();
      var path = Path.Combine(Sandbox.CloydsFolder.Folder.FullName, "directory-cleanup-handoffs", $"{receipt.CleanupId:N}.json");
      var handoffs = new WorktreeDirectoryCleanups(hub, Sandbox.CloydsFolder);
      var problems = new List<Exception>();
      using var inFlight = HoldCurrentDirectory(repository);
      try
      {
         new FileWrittenWhole(path).Write(JsonSerializer.Serialize(new
         {
            receipt.CleanupId, WorktreePath = root.Folder, RepositoryPath = repository.Folder,
            Git = new { inFlight.Id, Started = inFlight.StartTime.ToUniversalTime() }
         }));
         await handoffs.Recover(default, problems.Add);
         File.Exists(path).Must().BeTrue();
         problems.Count.Must().Be(0);
      }
      finally { await Release(inFlight); }
      await handoffs.Recover(default, problems.Add);
      problems.Count.Must().Be(1);
      File.Exists(path).Must().BeFalse();
      (await hub.OutstandingDirectoryCleanup(root, default))!.Status.Must().Be("needsReview");
      (await GitCommand.In(root, default, "branch", "--show-current")).CheckedOutput().Trim().Must().Be("replacement");
   }

   [XF] public async Task a_locked_rules_directory_keeps_its_parent_registered_until_its_empty_folder_is_gone()
   {
      if(!HasCleanup()) return;
      var project = Sandbox.Project("uses shared rules").Project;
      var git = GitRepositoryOfASpecification.In(new DirectoryInfo(project.Folder));
      git.Track(".gitignore", "/.claude-shared/");
      var root = new Project(Path.Combine(Sandbox.ProjectsFolder, "project topic"));
      git.HasAWorktreeIn(new DirectoryInfo(root.Folder), "topic");
      var catalog = Sandbox.Project("catalog").Project;
      var nested = SharedRulesCatalog.FolderIn(root);
      GitRepositoryOfASpecification.In(new DirectoryInfo(catalog.Folder)).HasAWorktreeIn(new DirectoryInfo(nested.Folder), "checkout/topic");
      var hub = Sandbox.HubOfTheSandbox.Hub;
      var operation = new RemoveDashboardWorktree(hub, Sandbox.CloydsFolder, project, root, new SharedRulesCatalog(catalog));
      using var holder = HoldCurrentDirectory(nested);
      try
      {
         var review = await operation.ReviewSessions(await operation.ReadGit(default), default);
         var result = await operation.Remove(review, new HashSet<Project> { nested, root }, false, default);
         result!.Contains("still registered").Must().BeTrue();
         (await hub.OutstandingDirectoryCleanup(nested, default))!.Status.Must().Be("pending");
         (await GitCommand.In(catalog, default, "show-ref", "--verify", "--quiet", "refs/heads/checkout/topic")).ExitCode.Must().Be(1);
         (await GitWorktrees.Read(project, default))!.RegisteredRoots.Contains(root).Must().BeTrue();
         (await operation.ReadGit(default)).CanRemove.Must().BeFalse();
      }
      finally { await Release(holder); }
   }

   [XF] public async Task a_reoccupied_rules_folder_is_protected_during_the_parent_operation_and_while_delivery_is_interrupted()
   {
      if(!HasCleanup()) return;
      var main = Sandbox.Project("formerly attached rules").Project;
      var git = GitRepositoryOfASpecification.In(new DirectoryInfo(main.Folder));
      git.Track(".gitignore", "/.claude-shared/");
      var root = new Project(Path.Combine(Sandbox.ProjectsFolder, "new topic"));
      git.HasAWorktreeIn(new DirectoryInfo(root.Folder), "topic");
      var catalog = Sandbox.Project("machine catalog").Project;
      var nested = SharedRulesCatalog.FolderIn(root);
      GitRepositoryOfASpecification.In(new DirectoryInfo(catalog.Folder)).HasAWorktreeIn(new DirectoryInfo(nested.Folder), "checkout/topic");
      var hub = Sandbox.HubOfTheSandbox.Hub;
      var ticket = (await hub.PrepareDirectoryCleanup(nested, default))!;
      var service = new RemoveDashboardWorktree(hub, Sandbox.CloydsFolder, main, root, new SharedRulesCatalog(catalog));
      var plan = await service.ReadGit(default);
      var keep = Path.Combine(nested.Folder, "keep.txt");
      var checks = 0;
      await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => plan.Remove(new HashSet<Project>(), () =>
      {
         if(++checks == 2)
         {
            Directory.CreateDirectory(nested.Folder);
            File.WriteAllText(keep, "New contents");
         }
         return Task.CompletedTask;
      }, default));
      File.Exists(keep).Must().BeTrue();
      (await GitWorktrees.Read(main, default))!.RegisteredRoots.Contains(root).Must().BeTrue();
      var path = Path.Combine(Sandbox.CloydsFolder.Folder.FullName, "directory-cleanup-handoffs", $"{ticket.CleanupId:N}.json");
      new FileWrittenWhole(path).Write(JsonSerializer.Serialize(new { ticket.CleanupId, ticket.WorktreePath, RepositoryPath = catalog.Folder }));
      (await service.ReadGit(default)).CanRemove.Must().BeFalse();
      File.Delete(keep);
      Directory.Delete(nested.Folder, recursive: false);
      (await service.ReadGit(default)).CanRemove.Must().BeFalse();
      File.Delete(path);
      var parentOnly = await service.ReadGit(default);
      await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => parentOnly.Remove(new HashSet<Project>(), () =>
      {
         Directory.CreateDirectory(nested.Folder);
         File.WriteAllText(keep, "Reused after the parent-only review");
         return Task.CompletedTask;
      }, default));
      File.Exists(keep).Must().BeTrue();
   }

   bool HasCleanup()
   {
      if(Sandbox.HubOfTheSandbox.Hub.SupportsWorktreeDirectoryCleanup) return true;
      if(Environment.GetEnvironmentVariable("CLOYD_REQUIRE_DIRECTORY_CLEANUP") == "1") throw new InvalidOperationException("The paired hub must support directory cleanup.");
      return false;
   }

   static Process HoldCurrentDirectory(Project folder)
   {
      var start = new ProcessStartInfo("pwsh.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "[Console]::ReadLine() | Out-Null"])
         { WorkingDirectory = folder.Folder, RedirectStandardInput = true, CreateNoWindow = true };
      return Process.Start(start) ?? throw new InvalidOperationException("Could not start the private directory holder.");
   }

   static async Task Release(Process holder)
   {
      holder.StandardInput.Close();
      await holder.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
   }
}
