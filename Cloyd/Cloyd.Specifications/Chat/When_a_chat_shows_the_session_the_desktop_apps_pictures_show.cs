using Avalonia.Controls;
using Cloyd.Chat;
using Cloyd.Chat.ToolCalls;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat;

/// <summary>
/// A chat showing the session whose conversation the pictures of the Claude desktop app show
/// (<c>dev_docs/cloyd-chat-specification.md</c>), as the sandbox hub serves it: drawn to
/// <c>RenderedWindows/the-sample-session.png</c>, to set beside them.
/// </summary>
public class When_a_chat_shows_the_session_the_desktop_apps_pictures_show : SpecificationOpeningWindows
{
   readonly ChatOfASession _chat;

   public When_a_chat_shows_the_session_the_desktop_apps_pictures_show()
   {
      var session = TheSampleSession.InTheSandbox();
      var window = Open(() => new Window { Width = 840, Height = 7000, Content = new ChatOfASession(Sandbox.Started.HubOfTheSandbox.Hub) });
      _chat = OnTheUIThread(() => (ChatOfASession)window.Content!);
      OnTheUIThread(() => _chat.Show(session));
      WaitUntil(() => _chat.Shown.Count > 0, "the chat showed the conversation");
      Render(window, "the-sample-session");
   }

   List<ShownInTheChat> Shown => OnTheUIThread(() => _chat.Shown.ToList());

   IReadOnlyList<ShownInTheChat.RunOfToolCalls> Runs => Shown.OfType<ShownInTheChat.RunOfToolCalls>().ToList();

   [XF] public void it_shows_magnuss_six_prompts() => Shown.OfType<ShownInTheChat.Prompt>().Count().Must().Be(6);

   [XF] public void it_counts_the_first_run_of_tool_calls_with_the_one_that_failed() => Runs[0].Counted.Must().Be("Read 2 files, ran 2 commands, 1 failed");

   [XF] public void it_leaves_the_desktop_apps_own_tools_out_of_the_runs() => Runs[0].Calls.Count.Must().Be(4);

   [XF] public void it_counts_the_second_run_of_tool_calls() => Runs[1].Counted.Must().Be("Edited a file, created a file, ran a command");

   [XF] public void it_counts_the_lines_the_second_runs_edits_added() => Runs[1].LinesAdded.Must().Be(9);

   [XF] public void it_shows_the_first_command_without_a_description_after_its_cd() => Runs[0].Calls[2].Target.Must().Be("ls...");

   [XF] public void it_shows_a_command_by_the_description_claude_gave_it() => Runs[2].Calls.Single().Target.Must().Be("Run a 10-second sleep script in background");

   [XF] public void it_shows_no_line_for_the_background_tasks_notice() => Shown.OfType<ShownInTheChat.PromptFromElsewhere>().Must().BeEmpty();

   [XF] public void it_shows_the_read_after_the_notice_by_itself_after_claudes_answer_before_it()
   {
      var read = Shown.IndexOf(Runs[3]);
      Runs[3].IsOneCall.Must().BeTrue();
      Runs[3].Calls.Single().Target.Must().Be("bsog6f9eo.output");
      Shown[read - 1].Must().Satisfy(it => it is ShownInTheChat.ClaudesText { Markdown: "Started." });
   }

   [XF] public void it_shows_the_compact_command_before_the_compactions_line()
   {
      var compaction = Shown.OfType<ShownInTheChat.Compaction>().Single();
      Shown[Shown.IndexOf(compaction) - 1].Must().Satisfy(it => it is ShownInTheChat.SlashCommand { AsTyped: "/compact" });
   }

   [XF] public void it_tells_how_many_tokens_the_compaction_saved() => Shown.OfType<ShownInTheChat.Compaction>().Single().Line.Must().Be("Compacted session · saved 81.5K tokens");

   [XF] public void it_shows_the_write_magnus_denied_as_stopped()
   {
      var write = Runs[^1].Calls.Single();
      write.Outcome.Must().Be(OutcomeOfAToolCall.Stopped);
      $"{write.Verb} {write.Target}".Must().Be("Creating hello.txt");
   }

   [XF] public void it_shows_neither_claudes_thinking_nor_the_interruptions() => Shown.Count.Must().Be(20);

   [XF] public void it_follows_the_end_of_the_conversation() => OnTheUIThread(() => _chat.FollowsTheEnd).Must().BeTrue();
}
