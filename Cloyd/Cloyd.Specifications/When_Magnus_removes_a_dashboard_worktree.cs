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
         var removal = new RemoveDashboardWorktree(server.Hub, Sandbox.CloydsFolder, project, checkout);
         (await GitCommand.In(checkout, default, "-c", "user.name=Specification", "-c", "user.email=specification@localhost", "-c", "commit.gpgsign=false",
                              "commit", "--allow-empty", "-m", "A commit outside the integration branch")).CheckedOutput();
         var reviewed = await removal.ReviewSessions(await removal.ReadGit(default), default);
         Directory.Exists(checkout.Folder).Must().BeTrue();
         (server.Applied is null).Must().BeTrue();
         server.Result = new(preview.OperationId, "blocked", [], [], [new("A session started while the confirmation dialog was open.")]);
         await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => removal.Remove(reviewed, new HashSet<Project>(), disposition == "recoverableDelete", default));
         Directory.Exists(checkout.Folder).Must().BeTrue();
         server.Applied!["operationId"]!.GetValue<Guid>().Must().Be(preview.OperationId);
         server.ResetApplied();
         server.Result = new(preview.OperationId, "completed", sessions.Select(it => new SessionDispositionReceipt(it.AgentCli, it.SessionId,
            $"{it.AgentCli}:{it.SessionId}", disposition, disposition == "recoverableDelete" ? DateTimeOffset.UtcNow.AddDays(7) : null)).ToArray(), []);
         await removal.Remove(reviewed, new HashSet<Project>(), disposition == "recoverableDelete", default);
         Directory.Exists(checkout.Folder).Must().BeFalse();
         server.Applied!["operationId"]!.GetValue<Guid>().Must().Be(preview.OperationId);
         server.Applied["disposition"]!.GetValue<string>().Must().Be(disposition);
         server.Applied["sessions"]!.AsArray().Select(it => (it!["agentCli"]!.GetValue<string>(), it["sessionId"]!.GetValue<string>(), it["hostId"]?.GetValue<string>()))
            .Must().SequenceEqual(sessions.Select(it => (it.AgentCli, it.SessionId, it.HostId)));
         server.Applied["shellOnlyHostIds"]!.AsArray().Count.Must().Be(0);

      }
   }

   [XF] public async Task dirty_files_and_waiting_for_input_each_prevent_the_archive_and_Git_removal()
   {
      var project = Sandbox.Project("dirty").Project;
      var checkout = new Project(Path.Combine(Sandbox.ProjectsFolder, "dirty-checkout"));
      GitRepositoryOfASpecification.In(new DirectoryInfo(project.Folder)).HasAWorktreeIn(new DirectoryInfo(checkout.Folder), "dirty-checkout");
      var preview = new WorktreeSessionsPreview(Guid.NewGuid(), checkout.Folder, [], [], []);
      await using var server = new StandInWorktreeSessionArchive(new DirectoryInfo(Path.Combine(project.Folder, "api")), preview);
      var removal = new RemoveDashboardWorktree(server.Hub, Sandbox.CloydsFolder, project, checkout);
      var reviewed = await removal.ReviewSessions(await removal.ReadGit(default), default);
      File.WriteAllText(Path.Combine(checkout.Folder, "keep.txt"), "uncommitted");
      await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => removal.Remove(reviewed, new HashSet<Project>(), false, default));
      var dirty = await removal.ReviewSessions(await removal.ReadGit(default), default);
      dirty.CanRemove.Must().BeFalse();
      (server.Applied is null).Must().BeTrue();
      File.Exists(Path.Combine(checkout.Folder, "keep.txt")).Must().BeTrue();

      var waiting = preview with { Sessions = [new WorktreeSession("codex", "session", "host", "waitingForInput")] };
      var occupied = new WorktreeRemovalReview(reviewed.ReviewedHub, reviewed.Git, [waiting], [WorktreeOccupants.From(waiting, null)], []);
      occupied.CanRemove.Must().BeFalse();
      await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => removal.Remove(occupied, new HashSet<Project>(), false, default));
   }

   [XF] public async Task stop_idle_sends_only_eligible_reviewed_targets_and_explains_a_last_minute_skip()
   {
      var root = Sandbox.Project("agent actions").Project;
      var folder = new Project(Path.Combine(Sandbox.ProjectsFolder, "topic"));
      GitRepositoryOfASpecification.In(new DirectoryInfo(root.Folder)).HasAWorktreeIn(new DirectoryInfo(folder.Folder), "topic");
      var idle = new OccupantTarget("0000000000000001", 101, "codex", "idle", 201, DateTime.UtcNow);
      var waiting = new OccupantTarget("0000000000000002", 102, "codex", "waiting", 202, DateTime.UtcNow);
      var drafted = new OccupantTarget("0000000000000003", 103, null, null, null, null);
      var preview = new WorktreeSessionsPreview(Guid.NewGuid(), folder.Folder, [new("codex", "idle", idle.HostId, "idle"),
         new("codex", "waiting", waiting.HostId, "waitingForInput")], [new(drafted.HostId!)], []);
      var occupants = new ReviewedOccupants([new(idle, "idle", "stopAgent", null, "Idle helper"),
         new(waiting, "waitingForInput", null, "Waiting for input.", "Waiting helper"), new(drafted, "idle", "closeShell", null)]);
      await using var server = new StandInWorktreeSessionArchive(new DirectoryInfo(Path.Combine(root.Folder, "api")), preview, occupants);
      var service = new RemoveDashboardWorktree(server.Hub, Sandbox.CloydsFolder, root, folder);
      var window = Open(() => new WorktreeRemovalDialog(service, it => it.SessionId, _ => {}, id => id.Value == drafted.HostId, default));
      Button? stop = null;
      WaitUntil(() => (stop = Button("Stop idle sessions (1)"))?.IsEnabled == true, "the review exposed only the idle agent");
      OnTheUIThread(() => Button("Close idle shell tabs (0)")!.IsEnabled).Must().BeFalse();
      Click(stop!);
      WaitUntil(() => window.GetVisualDescendants().OfType<TextBlock>().Any(it => it.Text?.Contains("Activity changed after review.") == true),
                "the action's actual outcome was shown");
      server.OccupantRequest!["action"]!.GetValue<string>().Must().Be("stopAgent");
      server.OccupantRequest!["targets"]!.Deserialize<OccupantTarget[]>(JsonSerializerOptions.Web)!.Must().SequenceEqual([idle]);
      Directory.Exists(folder.Folder).Must().BeTrue();
      (server.Applied is null).Must().BeTrue();
      Button? Button(string content) => window.GetVisualDescendants().OfType<Button>().SingleOrDefault(it => Equals(it.Content, content));
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

   [XF] public async Task a_lost_Delete_receipt_is_recovered_explicitly_before_a_new_Archive_can_be_confirmed()
   {
      var project = Sandbox.Project("previous receipt").Project;
      var folder = new Project(Path.Combine(Sandbox.ProjectsFolder, "pending"));
      GitRepositoryOfASpecification.In(new DirectoryInfo(project.Folder)).HasAWorktreeIn(new DirectoryInfo(folder.Folder), "pending");
      var id = Guid.NewGuid().ToString();
      var preview = new WorktreeSessionsPreview(Guid.NewGuid(), folder.Folder, [new("codex", id, null, "closed")], [], []);
      await using var server = new StandInWorktreeSessionArchive(new DirectoryInfo(Path.Combine(project.Folder, "api")), preview);
      server.Result = new(preview.OperationId, "completed", [new("codex", id, "archive-id", "recoverableDelete", DateTimeOffset.UtcNow.AddDays(7))], []);
      var operation = new RemoveDashboardWorktree(server.Hub, Sandbox.CloydsFolder, project, folder);
      var initial = await operation.ReviewSessions(await operation.ReadGit(default), default);
      server.LoseNextReceipt = true;
      await Xunit.Assert.ThrowsAsync<HttpRequestException>(() => operation.Remove(initial, new HashSet<Project>(), true, default));
      server.ResetApplied();
      var reopen = await operation.ReviewSessions(await operation.ReadGit(default), default);
      reopen.Pending.Single().Disposition.Must().Be("recoverableDelete");
      reopen.CanRemove.Must().BeFalse();
      await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => operation.Remove(reopen, new HashSet<Project>(), false, default));
      (server.Applied is null).Must().BeTrue();
      await operation.Recover(reopen.Pending.Single());
      server.Applied!["disposition"]!.GetValue<string>().Must().Be("recoverableDelete");
      Directory.Exists(folder.Folder).Must().BeTrue();
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
