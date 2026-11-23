using Avalonia.Controls;
using Avalonia.VisualTree;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.ProjectsDashboard;
using Cloyd.Saving;
using Cloyd.Specifications.TheSandbox;
using Avalonia.Automation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cloyd.Hub;
using Cloyd.Codex;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_Magnus_removes_a_dashboard_worktree : SpecificationOpeningWindows
{
   [XF] public async Task confirmation_sends_only_the_reviewed_sessions_and_requires_the_hubs_receipt_before_removal()
   {
      foreach(var disposition in new[] { "archive", "recoverableDelete" })
      {
         var project = Sandbox.Project(disposition).Project;
         var checkout = new Project(Path.Combine(Sandbox.ProjectsFolder, disposition + "-checkout"));
         var git = GitRepositoryOfASpecification.In(new DirectoryInfo(project.Folder));
         git.HasAWorktreeIn(new DirectoryInfo(checkout.Folder), "feature");
         var sessions = new[] { new WorktreeSession("codex", Guid.NewGuid().ToString(), null, "closed"),
                                new WorktreeSession("claudeCode", Guid.NewGuid().ToString(), null, "closed") };
         var preview = new WorktreeSessionsPreview(Guid.NewGuid(), checkout.Folder, sessions, [], []);
         await using var server = new StandInWorktreeSessionArchive(new DirectoryInfo(Path.Combine(project.Folder, "api")), preview);
         var reviewed = await server.Hub.PreviewWorktreeSessions(checkout, default);
         var owner = Open(() => new Window());
         var removal = new RemoveDashboardWorktree(owner, server.Hub, Sandbox.CloydsFolder, _ => throw new InvalidOperationException("Unexpected dirty worktree"));
         (await GitCommand.In(checkout, default, "-c", "user.name=Specification", "-c", "user.email=specification@localhost", "-c", "commit.gpgsign=false",
                              "commit", "--allow-empty", "-m", "A commit outside the integration branch")).CheckedOutput();
         await ConfirmUntil("Cancel");
         Directory.Exists(checkout.Folder).Must().BeTrue();
         (server.Applied is null).Must().BeTrue();
         var button = disposition == "archive" ? "Archive" : "Delete";
         server.Result = new(preview.OperationId, "blocked", [], [], [new("A session started while the confirmation dialog was open.")]);
         await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => ConfirmUntil(button));
         Directory.Exists(checkout.Folder).Must().BeTrue();
         server.Result = new(preview.OperationId, "completed", sessions.Select(it => new SessionDispositionReceipt(it.AgentCli, it.SessionId,
            $"{it.AgentCli}:{it.SessionId}", disposition, disposition == "recoverableDelete" ? DateTimeOffset.UtcNow.AddDays(7) : null)).ToArray(), []);
         await ConfirmUntil(button);
         Directory.Exists(checkout.Folder).Must().BeFalse();
         server.Applied!["operationId"]!.GetValue<Guid>().Must().Be(preview.OperationId);
         server.Applied["disposition"]!.GetValue<string>().Must().Be(disposition);
         server.Applied["sessions"]!.AsArray().Select(it => (it!["agentCli"]!.GetValue<string>(), it["sessionId"]!.GetValue<string>(), it["hostId"]?.GetValue<string>()))
            .Must().SequenceEqual(sessions.Select(it => (it.AgentCli, it.SessionId, it.HostId)));
         server.Applied["shellOnlyHostIds"]!.AsArray().Count.Must().Be(0);

         Task ConfirmUntil(string choice)
         {
            var pending = OnTheUIThread(() => removal.Run(project, checkout, reviewed, default));
            PressDialogButton("OK", pending);
            PressDialogButton(choice, pending);
            WaitUntil(() => pending.IsCompleted, "the reviewed worktree operation ended");
            return pending;
         }

         void PressDialogButton(string label, Task pending)
         {
            Button? found = null;
            WaitUntil(() =>
            {
               if(pending.IsFaulted) pending.GetAwaiter().GetResult();
               found = owner.OwnedWindows.OfType<WorktreeRemovalDialog>().SingleOrDefault()?.GetVisualDescendants()
                            .OfType<Button>().SingleOrDefault(it => Equals(it.Content, label));
               return found is not null;
            }, $"the dialog offered {label}", () => $"operation {pending.Status}, {owner.OwnedWindows.Count} owned windows", TimeSpan.FromSeconds(6));
            Click(found!);
         }
      }
   }

   [XF] public async Task dirty_files_and_waiting_for_input_each_prevent_the_archive_and_Git_removal()
   {
      var project = Sandbox.Project("dirty").Project;
      var checkout = new Project(Path.Combine(Sandbox.ProjectsFolder, "dirty-checkout"));
      GitRepositoryOfASpecification.In(new DirectoryInfo(project.Folder)).HasAWorktreeIn(new DirectoryInfo(checkout.Folder), "dirty-checkout");
      var preview = new WorktreeSessionsPreview(Guid.NewGuid(), checkout.Folder, [], [], []);
      await using var server = new StandInWorktreeSessionArchive(new DirectoryInfo(Path.Combine(project.Folder, "api")), preview);
      var owner = Open(() => new Window());
      var changesShown = new List<Project>();
      var removal = new RemoveDashboardWorktree(owner, server.Hub, Sandbox.CloydsFolder, it => changesShown.Add(it.Checkout.Root));
      var waiting = preview with { Sessions = [new WorktreeSession("codex", Guid.NewGuid().ToString(), null, "waitingForInput")] };
      await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => OnTheUIThread(() => removal.Run(project, checkout, waiting, default)));
      File.WriteAllText(Path.Combine(checkout.Folder, "keep.txt"), "uncommitted");
      DoneOnTheUIThread(() => removal.Run(project, checkout, preview, default));
      changesShown.Must().SequenceEqual([checkout]);
      (server.Applied is null).Must().BeTrue();
      File.Exists(Path.Combine(checkout.Folder, "keep.txt")).Must().BeTrue();
   }

   [XF] public void the_menu_disables_removal_until_all_tabs_are_closed_and_agents_stopped()
   {
      var checkout = Sandbox.Project("feature").Project;
      var row = new DashboardWorktree(new GitWorktree(checkout, "feature", false));
      row.UpdateRemovalAvailability();
      var vacant = new WorktreeSessionsPreview(Guid.NewGuid(), checkout.Folder, [], [], []);
      var occupied = new[] {
         vacant with { ShellOnlyHosts = [new WorktreeShell("host")] },
         vacant with { Sessions = [new WorktreeSession("codex", "session", "host", "idle")] },
         vacant with { Sessions = [new WorktreeSession("claude", "external", null, "idle")] },
         vacant with { Sessions = [new WorktreeSession("codex", "error", null, "error")] }
      };
      var previews = new Queue<WorktreeSessionsPreview>(occupied.Append(vacant));
      WorktreeSessionsPreview? selected = null;
      var menu = OnTheUIThread(() => new WorktreeContextMenu(row, failure => throw failure, _ => Task.FromResult(previews.Dequeue()), it => selected = it));
      var owner = Open(() => new Window { Content = new Button { Content = "feature", ContextMenu = menu } });
      var remove = OnTheUIThread(() => menu.Items.OfType<MenuItem>().Single(it => Equals(it.Header, "Remove worktree")));
      foreach(var _ in occupied)
      {
         OnTheUIThread(() => menu.Open((Control)owner.Content!));
         WaitUntil(() => AutomationProperties.GetHelpText(remove)?.Contains("Close all session tabs") == true, "the menu explained which worktree was in use");
         OnTheUIThread(() => remove.IsEffectivelyEnabled).Must().BeFalse();
         OnTheUIThread(menu.Close);
      }
      OnTheUIThread(() => menu.Open((Control)owner.Content!));
      WaitUntil(() => remove.IsEffectivelyEnabled && remove.Command!.CanExecute(null), "a fresh preview enabled removal after all tabs and agents closed");
      OnTheUIThread(() => remove.Command!.Execute(null));
      selected!.OperationId.Must().Be(vacant.OperationId);
      OnTheUIThread(menu.Close);
   }

   [XF] public void a_saved_tab_that_can_resume_later_leaves_its_worktree_available()
   {
      var checkout = new Project(Path.GetTempPath());
      var worktree = new DashboardWorktree(new GitWorktree(checkout, "feature", false));
      var tab = new DashboardSession("saved");
      tab.Update(null, null, new CodexSession(new CodexSessionId("saved"), "Saved tab"), AgentCli.Codex, checkout, open: true, restorable: true);
      worktree.Children.Add(tab);
      worktree.UpdateRemovalAvailability();
      tab.OpenInProjectTab.Must().BeTrue();
      worktree.CanRemove.Must().BeTrue();
   }

   [XF] public async Task a_lost_receipt_replays_the_original_disposition_after_reopening()
   {
      var folder = Sandbox.Project("receipt").Project;
      var sessions = new[] { new WorktreeSession("codex", Guid.NewGuid().ToString(), null, "closed") };
      var preview = new WorktreeSessionsPreview(Guid.NewGuid(), folder.Folder, sessions, [], []);
      var until = DateTimeOffset.UtcNow.AddDays(7);
      var receipt = new SessionDispositionReceipt("codex", sessions[0].SessionId, "entry", "recoverableDelete", until);
      await using var server = new StandInWorktreeSessionArchive(new DirectoryInfo(Path.Combine(folder.Folder, "api")), preview);
      server.Result = new(preview.OperationId, "completed", [receipt], []);
      server.LoseNextReceipt = true;
      var pending = new PendingWorktreeDisposition(Sandbox.CloydsFolder, folder);
      await Xunit.Assert.ThrowsAsync<HttpRequestException>(() => pending.Apply(server.Hub, preview, "recoverableDelete"));
      var firstPayload = server.Applied!.ToJsonString();
      var reopened = new PendingWorktreeDisposition(Sandbox.CloydsFolder, folder);
      (await reopened.Reconcile(server.Hub)).Must().BeTrue();
      server.Applied!.ToJsonString().Must().Be(firstPayload);
      (await reopened.Reconcile(server.Hub)).Must().BeFalse();
   }

   [XF] public void a_partial_or_inconsistent_archive_receipt_keeps_the_checkout()
   {
      var sessions = new[] { new WorktreeSession("codex", "one", null, "closed"), new WorktreeSession("claudeCode", "two", null, "closed") };
      var preview = new WorktreeSessionsPreview(Guid.NewGuid(), Path.GetTempPath(), sessions, [], []);
      var entry = new SessionDispositionReceipt("codex", "one", "first", "archive", null);
      var receipt = new WorktreeSessionsResult(preview.OperationId, "partial", [entry], [], [new("The second session could not be archived.")]);
      receipt.ValidateReceipts(preview, "archive");
      Xunit.Assert.Throws<InvalidOperationException>(() => receipt.RequireComplete(preview));
      var missing = receipt with { Status = "completed", Blockers = [] };
      Xunit.Assert.Throws<InvalidDataException>(() => missing.RequireComplete(preview));
      var duplicate = missing with { Results = [entry, entry with { AgentCli = "claudeCode", SessionId = "two" }] };
      Xunit.Assert.Throws<InvalidDataException>(() => duplicate.ValidateReceipts(preview, "archive"));
      var unexpectedClose = receipt with { ClosedHostIds = ["unexpected"] };
      Xunit.Assert.Throws<InvalidDataException>(() => unexpectedClose.ValidateReceipts(preview, "archive"));
   }

   [XF] public async Task reopening_an_old_host_closure_request_requires_a_fresh_review()
   {
      var folder = Directory.CreateTempSubdirectory("cloyd-old-closure-");
      try
      {
         var checkout = new Project(folder.FullName);
         var cloyd = new CloydsFolder(folder);
         var preview = new WorktreeSessionsPreview(Guid.NewGuid(), checkout.Folder, [], [new("host")], []);
         var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(checkout.Folder.ToUpperInvariant())));
         var saved = new FileWrittenWhole(Path.Combine(folder.FullName, "worktree-dispositions", key + ".json"));
         saved.Write(JsonSerializer.Serialize(new { Preview = preview, Disposition = (string?)null }));
         var pending = new PendingWorktreeDisposition(cloyd, checkout);
         var noHubListening = TheHub.AnnouncedIn(folder);
         (await pending.Reconcile(noHubListening)).Must().BeTrue();
         saved.Text().Must().BeNull();
         (await pending.Reconcile(noHubListening)).Must().BeFalse();
      }
      finally { folder.Delete(recursive: true); }
   }
}
