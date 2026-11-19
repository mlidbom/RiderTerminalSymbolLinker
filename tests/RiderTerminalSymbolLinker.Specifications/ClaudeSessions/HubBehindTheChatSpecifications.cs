using Compze.Must;
using Compze.xUnitBDD;
using Xunit;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

// The chat of a tab with the sandbox's hub behind it, as outside the sandbox: the hub reads the conversation from the
// transcript of the tab's fake Claude Code, which answers each line it's given with "Heard: <the line>". The lines are
// typed into the terminal: the fake draws no input box for the hub to type the chat's messages into.

public class When_the_hub_is_behind_a_tabs_chat(When_the_hub_is_behind_a_tabs_chat.Story story) : IClassFixture<When_the_hub_is_behind_a_tabs_chat.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen OnceClaudeCodeRuns;
      public readonly ChatSeen OnceALineWasSaid;
      public readonly ChatSeen OnceAnotherWas;

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
      }
   }

   [XF] public void the_tab_shows_its_chat_once_claude_code_runs() => story.OnceClaudeCodeRuns.Card.Must().Be("CHAT");
   [XF] public void the_status_line_says_claude_is_idle() => story.OnceClaudeCodeRuns.StatusLine.Must().Be("Claude is idle");
   [XF] public void the_conversation_is_empty_until_something_is_said() => story.OnceClaudeCodeRuns.Transcript.Must().Be("");

   [XF] public void the_chat_shows_what_was_said_and_claude_codes_answer() =>
      story.OnceALineWasSaid.Transcript.Must().Be("## You\nFix the bug in the circle\n\n## Claude\nHeard: Fix the bug in the circle\n\n");

   [XF] public void and_what_was_said_next_after_it() =>
      story.OnceAnotherWas.Transcript.Must().Be(story.OnceALineWasSaid.Transcript + "## You\nAnd the square\n\n## Claude\nHeard: And the square\n\n");
}
