using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat.Status;
using Cloyd.Codex.Chat;
using Cloyd.Codex.Chat.Status;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>Two calls overlap after an earlier command finishes. A chat opened during the turn
/// uses Codex's timestamps, then renders the same end line from the final update or saved history.</summary>
public class When_Codex_reports_turn_and_tool_progress
{
   static readonly DateTimeOffset Started = new(2026, 11, 22, 5, 49, 30, TimeSpan.Zero);
   static readonly CodexChatItem Writing = new("reply", "agentMessage", "Partial reply.", "commentary") { Lifecycle = CodexChatItemLifecycle.InProgress };
   static readonly CodexChatItem Finished = new("first", "commandExecution", "done", null) { Lifecycle = CodexChatItemLifecycle.Completed, Details = new() { Status = "inProgress" } };
   static readonly CodexChatItem Patch = new("patch", "fileChange", "a.cs", null) { StartedAt = Started.AddSeconds(10), Lifecycle = CodexChatItemLifecycle.InProgress };
   static readonly CodexChatItem Command = new("command", "commandExecution", "", null)
   {
      StartedAt = Started.AddSeconds(31), Lifecycle = CodexChatItemLifecycle.InProgress, Details = new() { Command = "first\nsecond\nthird\nfourth", Status = "inProgress" }
   };
   static readonly CodexChatTurn Turn = new("turn", "inProgress", [Finished, Patch, Command]) { StartedAt = Started };

   [XF] public void the_turn_counts_commands_and_its_time_from_the_original_start() =>
      CodexTurnProgress.From(Turn).CountsAt(Started.AddSeconds(39)).Must().Be(" (39s · 2 commands)");

   [XF] public void the_latest_active_call_reports_its_own_time_and_the_other_running_call()
   {
      var calls = CodexTurnProgress.From(Turn).Calls;
      calls.Count.Must().Be(2);
      calls[^1].HeadingAt(Started.AddSeconds(39), calls.Count - 1).Must().Be("Running command · 8s · 1 more running");
      calls[^1].PreviewOfInput.Must().Be("first\nsecond\nthird…");
   }

   [XF] public void the_phase_tracks_the_live_item_and_falls_back_to_working_after_its_completion()
   {
      PhaseOf([Writing with { Kind = "reasoning" }]).Must().Be("Thinking…");
      PhaseOf([Writing]).Must().Be("Writing…");
      PhaseOf([Writing with { Lifecycle = CodexChatItemLifecycle.Completed }]).Must().Be("Working…");
      PhaseOf([Writing, Command]).Must().Be("Running tools…");
      static string PhaseOf(CodexChatItem[] items) => CodexTurnProgress.From(Turn with { Items = items }).Activity;
   }

   [XF] public void reopening_saved_history_gives_the_same_precise_duration_and_local_end_time()
   {
      var ended = Started.AddSeconds(40);
      var completed = Turn with { Status = "completed", CompletedAt = ended, DurationMs = 39_100 };
      var chat = new CodexConversation(new CodexChatSnapshot("session", [Turn]));
      chat.Update(completed);
      var history = new CodexConversation(new CodexChatSnapshot("session", [completed]));
      var line = $"Worked for 39s · done {WhenItEnded.AtUtc(ended.UtcDateTime)}";
      chat.AsShown().Rows.Last().TextShown.Must().Be(line);
      history.AsShown().Rows.Last().TextShown.Must().Be(line);
      history.AsShown().Progress.Must().BeNull();
   }

   [XF] public void older_history_keeps_known_outcomes_and_omits_unrecorded_timing()
   {
      EndOfACodexTurn.From(new CodexChatTurn("old", "completed", [])).Must().BeNull();
      EndOfACodexTurn.From(new CodexChatTurn("old", "failed", []))!.TextShown.Must().Be("Turn failed");
      EndOfACodexTurn.From(new CodexChatTurn("old", "interrupted", []))!.TextShown.Must().Be("Turn interrupted");
   }

   [XF] public void image_generation_has_an_activity_and_a_clock_even_without_command_details()
   {
      var image = new CodexChatItem("drawing", "imageGeneration", "", null) { Lifecycle = CodexChatItemLifecycle.InProgress, StartedAt = Started };
      CodexTurnProgress.From(Turn with { Items = [image] }).Calls.Single().HeadingAt(Started.AddSeconds(12), 0).Must().Be("Generating image · 12s");
   }
}
