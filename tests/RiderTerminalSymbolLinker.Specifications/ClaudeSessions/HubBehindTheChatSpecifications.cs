using System.Text.RegularExpressions;
using Compze.Must;
using Compze.xUnitBDD;
using Xunit;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

// The chat of a tab with the sandbox's hub behind it, as outside the sandbox: the hub reads the conversation from the
// transcript of the tab's stand-in for Claude Code, which answers each prompt with "Heard: <the prompt>", and types what
// the chat sends into the stand-in's input box.

public class When_the_hub_is_behind_a_tabs_chat(When_the_hub_is_behind_a_tabs_chat.Story story) : IClassFixture<When_the_hub_is_behind_a_tabs_chat.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen OnceClaudeCodeRuns;
      public readonly ChatSeen OnceALineWasSaid;
      public readonly ChatSeen OnceAnotherWas;
      public readonly ChatSeen OnceSentFromTheChat;
      public readonly ChatSeen OnceAToolPrintedWindowsLines;
      public readonly ChatSeen OnceSaidAfterThem;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.HaveTheHubBehindTheChats();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         OnceClaudeCodeRuns = chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         view.Say(0, "Fix the bug in the circle");
         OnceALineWasSaid = chat.SeenOnce(it => it.Transcript.Contains("Heard: Fix the bug in the circle"), "the chat to show Claude Code's answer");
         view.Say(0, "And the square");
         OnceAnotherWas = chat.SeenOnce(it => it.Transcript.Contains("Heard: And the square"), "the chat to show Claude Code's second answer");
         chat.Type("And the triangle");
         chat.Press("CTRL_ENTER");
         OnceSentFromTheChat = chat.SeenOnce(it => it.Transcript.Contains("Heard: And the triangle") || it.WhyNotTyped is not null,
                                             "Claude Code to answer what the chat sent, or the chat to say why it didn't get it");
         // The stand-in records the tool's result with \r\n, as Windows' tools print and Claude Code keeps it.
         view.Say(0, "Run a tool that prints 2 lines");
         OnceAToolPrintedWindowsLines = chat.SeenOnce(it => it.Rows.Any(row => row.Body?.Contains("line 1") == true), "the chat to show the tool's call");
         view.Say(0, "And the circle again");
         OnceSaidAfterThem = chat.SeenOnce(it => it.Transcript.Contains("Heard: And the circle again"), "the chat to show what was said after the tool's lines");
      }
   }

   [XF] public void the_tab_shows_its_chat_once_claude_code_runs() => story.OnceClaudeCodeRuns.Card.Must().Be("CHAT");
   [XF] public void the_status_line_says_claude_is_idle() => story.OnceClaudeCodeRuns.StatusLine.Must().Be("Claude is idle");
   [XF] public void the_conversation_is_empty_until_something_is_said() => story.OnceClaudeCodeRuns.Rows.Must().BeEmpty();

   [XF] public void the_chat_shows_what_was_said_and_claude_codes_answer() =>
      story.OnceALineWasSaid.Rows.Select(it => (it.Kind, it.Text)).Must().Satisfy(it => it.SequenceEqual([("prompt", "Fix the bug in the circle"), ("claude", "Heard: Fix the bug in the circle")]));

   [XF] public void and_what_was_said_next_after_it() =>
      story.OnceAnotherWas.Transcript.Must().Be(story.OnceALineWasSaid.Transcript + "\nAnd the square\nHeard: And the square");

   [XF] public void what_the_chat_sends_claude_code_gets() => story.OnceSentFromTheChat.WhyNotTyped.Must().BeNull();

   [XF] public void and_answers() =>
      story.OnceSentFromTheChat.Transcript.Must().Be(story.OnceAnotherWas.Transcript + "\nAnd the triangle\nHeard: And the triangle");

   [XF] public void the_input_box_is_empty_again() => story.OnceSentFromTheChat.MessageBox.Must().Be("");

   [XF] public void a_tools_windows_line_endings_show_as_line_breaks() =>
      story.OnceAToolPrintedWindowsLines.Rows.Must().Satisfy(it => it.Any(row => row.Body?.Contains("line 1\nline 2") == true && !row.Body.Contains('\r')));

   [XF] public void and_the_chat_goes_on_after_them() => story.OnceSaidAfterThem.Transcript.Must().Satisfy(it => it.EndsWith("\nAnd the circle again\nHeard: And the circle again"));
}

// The chat of a tab with the sandbox's hub behind it while a turn is under way: the story tells the hub of the turn as a
// Claude Code session's tooling-commands mod does, while the stand-in for Claude Code is busy.

public class When_the_hub_tells_of_a_turn_under_way_in_a_tabs_chat(When_the_hub_tells_of_a_turn_under_way_in_a_tabs_chat.Story story)
   : IClassFixture<When_the_hub_tells_of_a_turn_under_way_in_a_tabs_chat.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen WhileItWrites;
      public readonly ChatSeen AsItWritesOn;
      public readonly ChatSeen OnceInterrupted;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.HaveTheHubBehindTheChats();
         var claudeCode = view.StartANewSession().ClaudeCode!.ProcessId;
         var chat = view.ChatOf(0);
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         view.Say(0, "Be busy for 30 seconds");
         chat.SeenOnce(it => it.StatusLine == "Claude is busy", "the chat to show that Claude is busy");

         var turn = Guid.NewGuid().ToString();
         var startedAt = DateTimeOffset.UtcNow;
         view.TellTheHubOfATurn(claudeCode, turn, startedAt,
                                new { kind = "turnStarted", at = Now() },
                                new { kind = "responseRequested", at = Now(), response = 0 },
                                new { kind = "text", at = Now(), response = 0, block = 0, offset = 0, text = "## Looking at it\n\nThe area is" });
         WhileItWrites = chat.SeenOnce(it => it.Rows.Any(row => row.Key.StartsWith("being-written")) && it.WorkingLine?.StartsWith("Writing") == true,
                                       "the chat to show what Claude writes, and that it does");

         view.TellTheHubOfATurn(claudeCode, turn, startedAt, new { kind = "text", at = Now(), response = 0, block = 0, offset = 29, text = " **wrong**." });
         AsItWritesOn = chat.SeenOnce(it => it.Rows[^1].Text.EndsWith("wrong."), "the chat to show more of what Claude writes");

         chat.Press("ESCAPE");
         view.TellTheHubOfATurn(claudeCode, turn, startedAt, new { kind = "turnEnded", at = Now(), reason = "aborted" });
         OnceInterrupted = chat.SeenOnce(it => it.Rows.Any(row => row.Kind == "interruption"), "the chat to show the interruption");

         static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
      }
   }

   [XF] public void what_claude_writes_shows_at_the_conversations_end_as_it_streams() =>
      (story.WhileItWrites.Rows[^1].Kind, story.WhileItWrites.Rows[^1].Text).Must().Be(("claude", "Looking at it\nThe area is"));

   [XF] public void the_working_line_says_claude_writes_with_the_turns_time_and_tokens() =>
      story.WhileItWrites.WorkingLine.Must().Satisfy(it => it is not null && Regex.IsMatch(it, @"^Writing… · \d+s · ↓ \d+ tokens$"));

   [XF] public void and_grows_as_more_streams() => story.AsItWritesOn.Rows[^1].Text.Must().Be("Looking at it\nThe area is wrong.");

   [XF] public void once_interrupted_the_interruption_shows_in_its_place() =>
      story.OnceInterrupted.Rows.Select(it => it.Kind).Must().Satisfy(it => it.SequenceEqual(["prompt", "interruption"]));
}
