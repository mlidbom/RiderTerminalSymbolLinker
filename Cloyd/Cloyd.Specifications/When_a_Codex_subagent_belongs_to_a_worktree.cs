using Avalonia.Controls;
using Avalonia.VisualTree;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects;
using Cloyd.ProjectsDashboard;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_a_Codex_subagent_belongs_to_a_worktree : SpecificationOpeningWindows
{
   readonly Project _root, _checkout;
   const string ParentId = "01a2009c-1d49-7f32-8ab7-ed623493aef1";
   const string ChildId = "01a20455-6257-7803-968b-68a9c1c23443";

   public When_a_Codex_subagent_belongs_to_a_worktree()
   {
      _root = Sandbox.Project("session families").Project;
      _checkout = new Project(Path.Combine(Sandbox.ProjectsFolder, "codex-helper-1"));
      GitRepositoryOfASpecification.In(new DirectoryInfo(_root.Folder)).HasAWorktreeIn(new DirectoryInfo(_checkout.Folder), "helper");
   }

   [XF] public async Task the_completed_helper_is_shown_under_the_idle_parent_and_only_the_parent_can_be_stopped()
   {
      var target = new OccupantTarget("0000000000000001", 100, "codex", ParentId, 101, DateTime.UtcNow);
      var preview = Preview(new("codex", ParentId, target.HostId, "idle"), Helper("completed", false));
      await using var server = Archive(preview, new ReviewedOccupants([new(target, "idle", "stopAgent", null)]));
      var window = OpenReview(server);
      WaitUntil(() => Shows(window, "Aristotle"), "the completed review helper appeared");
      OnTheUIThread(() =>
      {
         var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(it => it.Text).ToList();
         labels.Contains("cloyd-codex-1").Must().BeTrue();
         (labels.IndexOf("cloyd-codex-1") < labels.IndexOf("Aristotle")).Must().BeTrue();
         Shows(window, "Completed").Must().BeTrue();
         Shows(window, "Outside Cloyd").Must().BeFalse();
         Button(window, "Stop idle sessions (1)").IsEnabled.Must().BeTrue();
      });
   }

   [XF] public async Task a_completed_loaded_helper_leaves_the_checkout_removable_after_its_parent_exits()
   {
      var preview = Preview(new("codex", ParentId, null, "closed"), Helper("completed", false));
      await using var server = Archive(preview);
      server.Result = new(preview.OperationId, "completed", [new("codex", ParentId, "parent-archive-entry", "archive", null)], []);
      var window = OpenReview(server);
      WaitUntil(() => Shows(window, "Aristotle"), "the parent and its reviewed helper appeared after the terminal exited");
      OnTheUIThread(() =>
      {
         Shows(window, "cloyd-codex-1").Must().BeTrue();
         Button(window, "Remove worktree").IsEnabled.Must().BeTrue();
      });
      Click(OnTheUIThread(() => Button(window, "Remove worktree")));
      WaitUntil(() => !Directory.Exists(_checkout.Folder), "the loaded completed child left the final vacancy check clear");
      server.Applied!["sessions"]!.AsArray().Select(it => it!["sessionId"]!.GetValue<string>()).Must().SequenceEqual([ParentId]);
   }

   [XF] public async Task a_helpers_outstanding_work_is_named_and_blocks_removal_after_its_parent_exits()
   {
      var preview = Preview(new("codex", ParentId, null, "closed"), Helper("background", true, "A background terminal is running."));
      await using var server = Archive(preview);
      var window = OpenReview(server);
      WaitUntil(() => Shows(window, "A background terminal is running."), "the blocker was explained beside its named helper");
      OnTheUIThread(() =>
      {
         Shows(window, "Aristotle").Must().BeTrue();
         Button(window, "Remove worktree").IsEnabled.Must().BeFalse();
      });
      Xunit.Assert.Throws<InvalidOperationException>(() => preview.RequireReadyFor(_checkout));
   }

   [XF] public async Task queued_work_in_a_grandchild_seen_by_the_second_reading_blocks_the_parent_stop()
   {
      var target = new OccupantTarget("0000000000000001", 100, "codex", ParentId, 101, DateTime.UtcNow);
      var child = Helper("completed", false);
      var preview = Preview(new("codex", ParentId, target.HostId, "idle"), child);
      var grandchild = child with { SessionId = "grandchild", ParentSessionId = ChildId, Name = "Verification", ParentName = "Aristotle",
                                   Activity = "queued", BlocksRemoval = true, Reason = "Verification has queued input." };
      var eligibility = new ReviewedOccupants([new(target, "idle", "stopAgent", null, "cloyd-codex-1")]) { Descendants = [child, grandchild] };
      await using var server = Archive(preview, eligibility);
      var window = OpenReview(server);
      WaitUntil(() => Shows(window, "Verification"), "the review also showed a helper newly reported by occupant eligibility");
      OnTheUIThread(() =>
      {
         var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(it => it.Text).ToList();
         (labels.IndexOf("Aristotle") >= 0 && labels.IndexOf("Aristotle") < labels.IndexOf("Verification")).Must().BeTrue();
         Button(window, "Stop idle sessions (0)").IsEnabled.Must().BeFalse();
         Button(window, "Remove worktree").IsEnabled.Must().BeFalse();
      });
   }

   [XF] public async Task work_starting_after_an_empty_preview_is_visible_even_after_the_parent_exits()
   {
      var preview = new WorktreeSessionsPreview(Guid.NewGuid(), _checkout.Folder, [new("codex", ParentId, null, "closed")], [], []);
      var occupants = new ReviewedOccupants([]) { Descendants = [Helper("waitingForInput", true, "Aristotle is waiting for approval.")] };
      await using var server = Archive(preview, occupants);
      var window = OpenReview(server);
      WaitUntil(() => Shows(window, "Aristotle is waiting for approval."), "the later reading identified the newly waiting helper");
      OnTheUIThread(() => Button(window, "Remove worktree").IsEnabled).Must().BeFalse();
   }

   [XF] public void a_helper_in_the_shared_rules_belongs_to_its_project_parent_across_both_readings()
   {
      var parent = new WorktreeOccupant(new("host", 100, "codex", ParentId, 101, DateTime.UtcNow), "idle", "stopAgent", null);
      var child = Helper("working", true, "Aristotle is working.");
      var catalog = new Project(Path.Combine(_checkout.Folder, ".claude-shared"));
      var staleRow = new WorktreeOccupant(new(null, null, "codex", ChildId, null, null), "unknown", null, null);
      var project = new WorktreeOccupants(_checkout, [parent, staleRow], []);
      var helpers = new WorktreeSubagents([project, new WorktreeOccupants(catalog, [], [child])]);
      helpers.BlockingStopOf(parent).Must().Be(child);
      helpers.ChildrenOf(ParentId).Single().Must().Be(child);
      helpers.WorktreeOf(child).Must().Be(catalog);
      helpers.Reconcile(project).Rows.Must().SequenceEqual([parent]);
      var conflicting = new WorktreeSessionsPreview(Guid.NewGuid(), _checkout.Folder, [new("codex", ChildId, null, "closed")], [], []);
      Xunit.Assert.Throws<InvalidDataException>(() => helpers.RequireSeparateArchiveTargets(conflicting));
      var hostedChild = staleRow with { Target = staleRow.Target with { AgentPid = 203 }, Activity = "working" };
      var contradictory = new WorktreeSubagents([new WorktreeOccupants(_checkout, [parent, hostedChild], [Helper("completed", false)])]);
      contradictory.BlockingStopOf(parent)!.SessionId.Must().Be(ChildId);
      contradictory.Reconcile(new WorktreeOccupants(_checkout, [hostedChild], [])).Rows.Single().Action.Must().BeNull();
   }

   static bool Shows(Window window, string text) => window.GetVisualDescendants().OfType<TextBlock>().Any(it => it.Text == text);
   static Button Button(Window window, string label) => window.GetVisualDescendants().OfType<Button>().Single(it => Equals(it.Content, label));

   static WorktreeSubagent Helper(string activity, bool blocks, string? reason = null) =>
      new(ChildId, ParentId, "cloyd-codex-1", "Aristotle", "/root/standards_review", activity, blocks, reason);

   WorktreeSessionsPreview Preview(WorktreeSession parent, WorktreeSubagent child) =>
      new(Guid.NewGuid(), _checkout.Folder, [parent], [], []) { Descendants = [child] };

   StandInWorktreeSessionArchive Archive(WorktreeSessionsPreview preview, ReviewedOccupants? occupants = null) =>
      new(new DirectoryInfo(Path.Combine(_root.Folder, "api")), preview, occupants);

   WorktreeRemovalDialog OpenReview(StandInWorktreeSessionArchive server) => Open(() => new WorktreeRemovalDialog(
      new RemoveDashboardWorktree(server.Hub, Sandbox.CloydsFolder, _root, _checkout), it => it.SessionId, _ => {}, _ => false, default));
}
