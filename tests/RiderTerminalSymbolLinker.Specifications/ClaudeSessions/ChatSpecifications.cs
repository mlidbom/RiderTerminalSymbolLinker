using Compze.Must;
using Compze.xUnitBDD;
using Xunit;
using static RiderTerminalSymbolLinker.Specifications.ClaudeSessions.TheChat;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

// The chat of a tab, with the sandbox's stand-in behind it in place of the hub behind it, which doesn't tell of the
// conversation yet: the stories set the conversation and Claude Code's status, see what the chat sends, and have Claude
// Code not get it.

public class When_a_tab_with_a_chat_opens(When_a_tab_with_a_chat_opens.Story story) : IClassFixture<When_a_tab_with_a_chat_opens.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen BeforeClaudeCodeRuns;
      public readonly ChatSeen OnceClaudeCodeRuns;
      public readonly IReadOnlyList<string> TheSwitchsShortcutInTheDefaultKeymap;
      public readonly IReadOnlyList<string> TheSwitchsShortcutInVisualStudio2022;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         BeforeClaudeCodeRuns = chat.Seen;
         chat.SayTheStatus("IDLE");
         OnceClaudeCodeRuns = chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         TheSwitchsShortcutInTheDefaultKeymap = view.ActionsBoundTo(SwitchShortcut, "$default");
         TheSwitchsShortcutInVisualStudio2022 = view.ActionsBoundTo(SwitchShortcut, "Visual Studio 2022");
      }
   }

   const string SwitchShortcut = "ctrl alt shift Z";
   const string SwitchAction = "ClaudeSessions.SwitchBetweenTheChatAndTheTerminal";

   [XF] public void it_shows_its_terminal_until_claude_code_runs() => story.BeforeClaudeCodeRuns.Card.Must().Be("TERMINAL");
   [XF] public void then_its_chat() => story.OnceClaudeCodeRuns.Card.Must().Be("CHAT");
   [XF] public void the_button_offers_the_terminal_while_the_chat_shows() => story.OnceClaudeCodeRuns.Button.Must().Be("Show the Terminal");
   [XF] public void the_status_line_says_claude_is_idle() => story.OnceClaudeCodeRuns.StatusLine.Must().Be("Claude is idle");

   [XF] public void the_switchs_shortcut_is_its_alone_in_the_default_keymap() => story.TheSwitchsShortcutInTheDefaultKeymap.Must().Satisfy(it => it.SequenceEqual([SwitchAction]));
   [XF] public void and_in_visual_studio_2022s() => story.TheSwitchsShortcutInVisualStudio2022.Must().Satisfy(it => it.SequenceEqual([SwitchAction]));
}

public class When_the_chat_shows_a_conversation(When_the_chat_shows_a_conversation.Story story) : IClassFixture<When_the_chat_shows_a_conversation.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen Shown;
      public readonly ChatSeen OnceTheResultCame;
      public readonly ChatSeen WithLinks;
      public readonly ChatSeen AfterARewind;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("BUSY");
         object[] conversation =
         [
            Item.Prompt("p1", "Fix the bug in the circle"),
            Item.AssistantText("a1", "Looking at it."),
            Item.ToolCall("t1", "Bash", new Dictionary<string, string> { ["command"] = "git status", ["description"] = "Show the working tree" }, "On branch main"),
            Item.AssistantText("a2", "Fixed **Circle.Area** in Shapes/Circle.cs:7."),
            Item.Compaction("c1"),
            Item.Prompt("p2", "/compact"),
            Item.Prompt("p3", "Run the tests"),
            Item.ToolCall("t2", "Read", new Dictionary<string, string> { ["file_path"] = @"C:\Some\File.cs" }, "1  ```code with fences```"),
            Item.ToolCall("t3", "Bash", new Dictionary<string, string> { ["command"] = "dotnet test" }, result: null)
         ];
         Shown = chat.Give(conversation);
         chat.OpenTheFold(2);
         conversation[^1] = Item.ToolCall("t3", "Bash", new Dictionary<string, string> { ["command"] = "dotnet test" }, "Failed: 1 in Shapes/Circle.cs:7", isAnError: true);
         chat.Give(conversation);
         OnceTheResultCame = chat.SeenOnce(it => it.Transcript.Contains("Failed: 1"), "the tool call's result to show");
         WithLinks = chat.SeenOnce(it => it.Links.Count(link => link == "Shapes/Circle.cs:7") == 2, "the symbol linker's links to show");
         chat.Give([.. conversation[..^2], Item.AssistantText("a3", "Went another way.")]); // As a rewind does.
         AfterARewind = chat.SeenOnce(it => it.Transcript.Contains("Went another way."), "the conversation to go another way");
      }
   }

   [XF] public void a_prompt_shows_under_a_heading_of_the_users() => story.Shown.Transcript.Must().StartWith("## You\nFix the bug in the circle\n\n");
   [XF] public void claudes_turn_starts_with_a_heading_of_claudes() => story.Shown.Transcript.Must().Contain("\n\n## Claude\nLooking at it.\n\n");

   [XF] public void each_tool_call_shows_as_its_header() =>
      story.Shown.Folds.Select(it => it.Header).Must().Satisfy(it => it.SequenceEqual(["▸ Bash: git status", "▸ Read: C:\\Some\\File.cs", "▸ Bash: dotnet test"]));

   [XF] public void with_its_input_and_result_folded_away_below_it() =>
      story.Shown.Folds[0].Folded.Must().Be("\n```\ncommand: git status\ndescription: Show the working tree\n── result ──\nOn branch main\n```");

   [XF] public void collapsed() => story.Shown.Folds.Must().Satisfy(folds => folds.All(it => !it.Open));

   [XF] public void a_result_with_backticks_in_it_is_fenced_with_more() => story.Shown.Folds[1].Folded.Must().StartWith("\n````\n");
   [XF] public void a_tool_call_without_a_result_shows_it_runs() => story.Shown.Folds[2].Folded.Must().Contain("── running ──");
   [XF] public void a_compaction_shows_as_a_divider() => story.Shown.Transcript.Must().Contain("\n───── The conversation was compacted here ─────\n\n## You\n/compact\n\n");

   [XF] public void the_status_line_counts_the_commands_of_the_turn() => story.Shown.StatusLine.Must().Be("Claude is busy · 2 commands this turn");

   [XF] public void when_a_tool_calls_result_comes_it_shows_in_the_fold() => story.OnceTheResultCame.Folds[2].Folded.Must().Contain("── failed ──\nFailed: 1");
   [XF] public void which_stays_open_if_the_user_opened_it() => story.OnceTheResultCame.Folds[2].Open.Must().BeTrue();
   [XF] public void and_what_came_before_it_stays_as_it_was() => story.OnceTheResultCame.Transcript.Must().StartWith(story.Shown.Transcript[..story.Shown.Transcript.IndexOf("▸ Bash: dotnet test")]);

   [XF] public void the_console_filters_link_what_they_link_in_a_terminal() => story.WithLinks.Links.Must().Contain("Circle.Area").Contain("Shapes/Circle.cs:7");

   [XF] public void when_the_conversation_goes_another_way_what_it_left_goes() =>
      story.AfterARewind.Transcript.Must().Be(story.Shown.Transcript[..story.Shown.Transcript.IndexOf("▸ Read:")] + "Went another way.\n\n");

   [XF] public void with_its_links() => story.AfterARewind.Links.Must().Satisfy(it => it.SequenceEqual(["Circle.Area", "Shapes/Circle.cs:7"]));
}

public class When_the_user_sends_a_message_from_the_chat(When_the_user_sends_a_message_from_the_chat.Story story) : IClassFixture<When_the_user_sends_a_message_from_the_chat.Story>
{
   public sealed class Story
   {
      public readonly SentToClaudeCode Sent;
      public readonly ChatSeen After;
      public readonly ChatSeen WhileWritten;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.Type("First line");
         chat.Press("SHIFT_ENTER");
         chat.Type("second line, ");
         chat.Paste("pasted\r\ntext");
         chat.Type(" and ");
         chat.Paste("one");
         chat.Paste("two");
         chat.Type(" ");
         WhileWritten = chat.PasteAnImage();
         chat.Press("ENTER");
         Sent = chat.SentOnce(it => it.Messages.Count > 0, "the message to reach Claude Code");
         After = chat.Seen;
      }
   }

   [XF] public void shift_enter_starts_a_new_line() => story.WhileWritten.MessageBox.Must().Be("First line\nsecond line, pasted\ntext and onetwo [Image #1]");

   [XF] public void enter_sends_it_as_one_message() => story.Sent.Messages.Count.Must().Be(1);

   // Two pastes side by side stay two, as in Claude Code's own box.
   [XF] public void each_part_as_it_came_into_the_box_typed_pasted_or_an_image() =>
      story.Sent.Messages[0].Select(it => (it.Kind, it.Text)).Must().Satisfy(it => it.SequenceEqual([("typed", "First line\nsecond line, "), ("pasted", "pasted\ntext"), ("typed", " and "), ("pasted", "one"), ("pasted", "two"), ("typed", " "), ("image", "[Image #1]")]));

   [XF] public void the_image_as_a_file_of_its_own() => story.Sent.Messages[0][^1].Must().Satisfy(it => it.FileExists && it.File!.EndsWith(".png"));
   [XF] public void the_box_is_empty_after() => story.After.MessageBox.Must().Be("");
   [XF] public void the_chat_stays_shown() => story.After.Card.Must().Be("CHAT");
}

public class When_the_user_sends_a_slash_command_or_keys_from_the_chat(When_the_user_sends_a_slash_command_or_keys_from_the_chat.Story story)
   : IClassFixture<When_the_user_sends_a_slash_command_or_keys_from_the_chat.Story>
{
   public sealed class Story
   {
      public readonly SentToClaudeCode Sent;
      public readonly ChatSeen AfterTheCommand;
      public readonly ChatSeen OnceClaudeCodeIsBusy;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.Press("ESCAPE");
         chat.Press("SHIFT_TAB");
         chat.Press("SHIFT_TAB");
         chat.Type("/model");
         AfterTheCommand = chat.Press("ENTER");
         chat.SayTheStatus("BUSY");
         Thread.Sleep(500);
         OnceClaudeCodeIsBusy = chat.Seen;
         Sent = chat.Sent;
      }
   }

   [XF] public void escape_interrupts_claude_code() => story.Sent.Interruptions.Must().Be(1);
   [XF] public void shift_tab_changes_its_permission_mode() => story.Sent.PermissionModeChanges.Must().Be(2);
   [XF] public void the_command_is_sent_as_typed() => story.Sent.Messages.Single().Must().Satisfy(it => it.SequenceEqual([new MessagePartSeen("typed", "/model")]));
   [XF] public void the_tab_shows_the_terminal_where_it_shows_what_it_does() => story.AfterTheCommand.Card.Must().Be("TERMINAL");
   [XF] public void which_stays_shown_as_if_the_user_chose_it() => story.OnceClaudeCodeIsBusy.Card.Must().Be("TERMINAL");
}

public class When_claude_code_gets_nothing_of_a_message_from_the_chat(When_claude_code_gets_nothing_of_a_message_from_the_chat.Story story)
   : IClassFixture<When_claude_code_gets_nothing_of_a_message_from_the_chat.Story>
{
   public sealed class Story
   {
      public const string Why = "The Claude Code's record names no session yet. Try again in a moment.";
      public readonly ChatSeen AfterSending;
      public readonly SentToClaudeCode Sent;
      public readonly SentToClaudeCode SentAgain;
      public readonly ChatSeen AfterSendingAgain;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.HaveClaudeCodeGetNothingOfIt(Why, theTerminalShowsWhy: false);
         chat.Type("Look at ");
         chat.Paste("pasted");
         chat.Type(" and ");
         chat.PasteAnImage();
         chat.Press("ENTER");
         AfterSending = chat.SeenOnce(it => it.WhyNotTyped is not null, "the chat to say why Claude Code didn't get it");
         Sent = chat.Sent;
         chat.HaveClaudeCodeGetIt();
         chat.Press("ENTER");
         SentAgain = chat.SentOnce(it => it.Messages.Count > 0, "the message to reach Claude Code");
         AfterSendingAgain = chat.SeenOnce(it => it.WhyNotTyped is null, "the chat to stop saying why");
      }
   }

   [XF] public void claude_code_gets_none_of_it() => story.Sent.Messages.Must().BeEmpty();
   [XF] public void the_message_is_back_in_the_box() => story.AfterSending.MessageBox.Must().Be("Look at pasted and [Image #1]");
   [XF] public void the_chat_says_why() => story.AfterSending.WhyNotTyped.Must().Be(Story.Why);
   [XF] public void and_stays_shown() => story.AfterSending.Card.Must().Be("CHAT");

   [XF] public void sent_again_each_part_is_what_it_came_into_the_box_as() =>
      story.SentAgain.Messages.Single().Select(it => (it.Kind, it.Text)).Must().Satisfy(it => it.SequenceEqual([("typed", "Look at "), ("pasted", "pasted"), ("typed", " and "), ("image", "[Image #1]")]));

   [XF] public void the_image_still_its_file() => story.SentAgain.Messages.Single()[^1].FileExists.Must().BeTrue();
   [XF] public void once_claude_code_got_it_the_chat_no_longer_says_why() => story.AfterSendingAgain.WhyNotTyped.Must().BeNull();
}

public class When_what_keeps_claude_code_from_getting_a_message_shows_in_the_terminal(When_what_keeps_claude_code_from_getting_a_message_shows_in_the_terminal.Story story)
   : IClassFixture<When_what_keeps_claude_code_from_getting_a_message_shows_in_the_terminal.Story>
{
   public sealed class Story
   {
      public const string WhyLeftUnsent = "Enter was pressed, and the box still held the message 5 seconds later.";
      public readonly ChatSeen AfterARefusal;
      public readonly ChatSeen BackInTheChat;
      public readonly ChatSeen AfterLeftUnsent;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.HaveClaudeCodeGetNothingOfIt("Claude Code's own input box holds text.", theTerminalShowsWhy: true);
         chat.Type("Refused");
         chat.Press("ENTER");
         AfterARefusal = chat.SeenOnce(it => it.Card == "TERMINAL", "the tab to show the terminal");
         BackInTheChat = chat.Switch();
         chat.HaveClaudeCodeLeaveItUnsent(WhyLeftUnsent);
         chat.Press("ENTER");
         AfterLeftUnsent = chat.SeenOnce(it => it.WhyNotTyped == WhyLeftUnsent, "the chat to say why the message was left unsent");
      }
   }

   [XF] public void the_tab_shows_the_terminal_where_it_is() => story.AfterARefusal.Card.Must().Be("TERMINAL");
   [XF] public void with_the_message_back_in_the_chats_box() => story.AfterARefusal.MessageBox.Must().Be("Refused");
   [XF] public void a_message_left_unsent_in_claude_codes_own_box_shows_there() => (story.BackInTheChat.Card, story.AfterLeftUnsent.Card).Must().Be(("CHAT", "TERMINAL"));
   [XF] public void and_isnt_back_in_the_chats_box_too() => story.AfterLeftUnsent.MessageBox.Must().Be("");
   [XF] public void the_chat_says_why() => story.AfterLeftUnsent.WhyNotTyped.Must().Be(Story.WhyLeftUnsent);
}

public class When_claude_code_gets_nothing_of_a_message_with_more_sent_after_it(When_claude_code_gets_nothing_of_a_message_with_more_sent_after_it.Story story)
   : IClassFixture<When_claude_code_gets_nothing_of_a_message_with_more_sent_after_it.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen WhileTheFirstIsGiven;
      public readonly ChatSeen After;
      public readonly SentToClaudeCode Sent;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("BUSY");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.Hold();
         chat.Type("First");
         chat.Press("ENTER");
         chat.Type("Second");
         chat.Press("ENTER");
         chat.Press("ESCAPE");
         WhileTheFirstIsGiven = chat.Type("Third, not sent yet");
         chat.HaveClaudeCodeGetNothingOfIt("The tooling typed into the session for all of 10 minutes.", theTerminalShowsWhy: false);
         chat.LetGo();
         After = chat.SeenOnce(it => it.WhyNotTyped is not null, "the chat to say why Claude Code didn't get it");
         Thread.Sleep(500); // Time for what waited to reach Claude Code, were it given.
         Sent = chat.Sent;
      }
   }

   [XF] public void the_box_is_free_for_the_next_while_one_is_given() => story.WhileTheFirstIsGiven.MessageBox.Must().Be("Third, not sent yet");
   [XF] public void claude_code_gets_none_of_what_waited_after_it() => (story.Sent.Messages.Count, story.Sent.Interruptions).Must().Be((0, 0));
   [XF] public void the_messages_are_back_in_the_box_in_order_ahead_of_what_was_written_since() => story.After.MessageBox.Must().Be("First\nSecond\nThird, not sent yet");
}

public class When_claude_code_waits_for_the_user_in_a_dialog(When_claude_code_waits_for_the_user_in_a_dialog.Story story)
   : IClassFixture<When_claude_code_waits_for_the_user_in_a_dialog.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen WhileItWaits;
      public readonly ChatSeen OnceItGoesOn;
      public readonly ChatSeen TheTerminalTheUserChose;
      public readonly ChatSeen WhileItWaitsAgain;
      public readonly ChatSeen OnceItGoesOnAgain;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("BUSY");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.SayTheStatus("WAITING");
         WhileItWaits = chat.SeenOnce(it => it.Card == "TERMINAL", "the tab to show the terminal");
         chat.SayTheStatus("BUSY");
         OnceItGoesOn = chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat again");
         TheTerminalTheUserChose = chat.Switch();
         chat.SayTheStatus("WAITING");
         WhileItWaitsAgain = chat.Seen;
         chat.SayTheStatus("IDLE");
         Thread.Sleep(500);
         OnceItGoesOnAgain = chat.Seen;
      }
   }

   [XF] public void the_tab_shows_the_terminal_where_the_dialog_is() => story.WhileItWaits.Card.Must().Be("TERMINAL");
   [XF] public void the_status_line_says_so() => story.WhileItWaits.StatusLine.Must().Be("Claude is waiting for you in the terminal");
   [XF] public void and_the_chat_again_once_claude_code_goes_on() => story.OnceItGoesOn.Card.Must().Be("CHAT");
   [XF] public void the_button_shows_the_other_card() => story.TheTerminalTheUserChose.Card.Must().Be("TERMINAL");
   [XF] public void a_terminal_the_user_chose_stays_through_a_dialog() => (story.WhileItWaitsAgain.Card, story.OnceItGoesOnAgain.Card).Must().Be(("TERMINAL", "TERMINAL"));
}

public class When_the_plugin_reloads_with_chats_shown(When_the_plugin_reloads_with_chats_shown.Story story) : IClassFixture<When_the_plugin_reloads_with_chats_shown.Story>
{
   public sealed class Story
   {
      public readonly IReadOnlyList<SessionTab> Before;
      public readonly IReadOnlyList<SessionTab> After;
      public readonly ChatSeen ChatAfter;
      public readonly IReadOnlyList<string> ScreenAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.Give(Item.Prompt("p1", "Hello"));
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.Type("Written before the reload");
         Before = view.Tabs;
         view.ReloadThePlugin();
         view.WaitUntil(() => view.Tabs.Count == 1, "the tab to be back in the view");
         After = view.Tabs;
         ChatAfter = view.ChatOf(0).Seen;
         view.Say(0, "a line after the reload");
         ScreenAfter = view.Screen(0);
      }
   }

   [XF] public void the_tab_comes_back_with_its_processes_and_session() => story.After.Must().Satisfy(it => it.SequenceEqual(story.Before));
   [XF] public void with_a_chat_of_the_reloaded_plugins() => story.ChatAfter.Card.Must().NotBeNull();
   [XF] public void with_what_its_box_held() => story.ChatAfter.MessageBox.Must().Be("Written before the reload");
   [XF] public void and_its_terminal_still_takes_what_is_typed() => story.ScreenAfter.Must().Contain("> a line after the reload");
}

public class When_the_ide_restarts_with_a_message_written_in_a_chat(When_the_ide_restarts_with_a_message_written_in_a_chat.Story story)
   : IClassFixture<When_the_ide_restarts_with_a_message_written_in_a_chat.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen Before;
      public readonly ChatSeen After;
      public readonly SentToClaudeCode SentAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.Type("Look at ");
         chat.Paste("pasted\ntext");
         chat.Type(" and ");
         Before = chat.PasteAnImage();
         view.RestartTheIde(tabsToComeBack: 1);
         chat = view.ChatOf(0);
         After = chat.SeenOnce(it => it.MessageBox != "", "the box to hold what it held");
         chat.SayTheStatus("IDLE");
         chat.Press("ENTER");
         SentAfter = chat.SentOnce(it => it.Messages.Count == 1, "Claude Code to get the message");
      }
   }

   [XF] public void the_box_holds_what_it_held() => story.After.MessageBox.Must().Be("Look at pasted\ntext and [Image #1]");
   [XF] public void each_part_as_it_came_into_the_box() => story.After.MessageBoxParts.Must().Satisfy(it => it.SequenceEqual(story.Before.MessageBoxParts));
   [XF] public void with_its_caret_where_it_was() => story.After.Caret.Must().Be(story.Before.Caret);

   [XF] public void sent_each_part_is_what_it_came_into_the_box_as() =>
      story.SentAfter.Messages.Single().Select(it => (it.Kind, it.Text)).Must().Satisfy(it => it.SequenceEqual([("typed", "Look at "), ("pasted", "pasted\ntext"), ("typed", " and "), ("image", "[Image #1]")]));

   [XF] public void the_image_still_its_file() => story.SentAfter.Messages.Single()[^1].FileExists.Must().BeTrue();
}

public class When_the_ide_crashes_with_a_message_written_in_a_chat(When_the_ide_crashes_with_a_message_written_in_a_chat.Story story)
   : IClassFixture<When_the_ide_crashes_with_a_message_written_in_a_chat.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen After;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.Type("Written before the crash");
         Thread.Sleep(2500); // The chat is saved half a second after it last changed, the tabs every two seconds.
         view.CrashTheIdeAndStartItAgain(tabsToComeBack: 1);
         After = view.ChatOf(0).SeenOnce(it => it.MessageBox != "", "the box to hold what it held");
      }
   }

   [XF] public void the_box_holds_what_it_held_half_a_second_before() => story.After.MessageBox.Must().Be("Written before the crash");
}

public class When_the_ide_restarts_with_messages_waiting_to_be_given_to_claude_code(When_the_ide_restarts_with_messages_waiting_to_be_given_to_claude_code.Story story)
   : IClassFixture<When_the_ide_restarts_with_messages_waiting_to_be_given_to_claude_code.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen BeforeTheConversationIsKnown;
      public readonly ChatSeen WhenClaudeCodeDidNotGetTheOneBeingGiven;
      public readonly ChatSeen WhenClaudeCodeGotIt;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         foreach(var tab in new[] { 0, 1 })
         {
            view.StartANewSession();
            var chat = view.ChatOf(tab);
            chat.SayTheStatus("BUSY");
            chat.Give(Item.Prompt("p1", "Hello"));
            chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
            chat.Hold();
            chat.Type("Being given");
            chat.Press("ENTER");
            chat.Type("Waiting");
            chat.Press("ENTER");
            chat.Type("Written since");
         }
         view.RestartTheIde(tabsToComeBack: 2);
         var didNotGetIt = view.ChatOf(0);
         var gotIt = view.ChatOf(1);
         BeforeTheConversationIsKnown = didNotGetIt.SeenOnce(it => it.MessageBox != "", "the box to hold what it held");
         didNotGetIt.Give(Item.Prompt("p1", "Hello"));
         WhenClaudeCodeDidNotGetTheOneBeingGiven = didNotGetIt.SeenOnce(it => it.MessageBox!.StartsWith("Being given"), "the message being given to come back");
         gotIt.Give(Item.Prompt("p1", "Hello"), Item.Prompt("p2", "Being given"));
         Thread.Sleep(500); // Time for it to come back, were it to.
         WhenClaudeCodeGotIt = gotIt.Seen;
      }
   }

   [XF] public void what_waited_comes_back_into_the_box_at_once_ahead_of_what_was_written_since() => story.BeforeTheConversationIsKnown.MessageBox.Must().Be("Waiting\nWritten since");
   [XF] public void the_chat_says_it_wasnt_sent() => story.BeforeTheConversationIsKnown.WhyNotTyped.Must().Be("Not sent: the IDE closed before Claude Code got this message.");

   [XF] public void the_message_being_given_comes_back_ahead_of_them_when_the_conversation_shows_claude_code_didnt_get_it() =>
      story.WhenClaudeCodeDidNotGetTheOneBeingGiven.MessageBox.Must().Be("Being given\nWaiting\nWritten since");

   [XF] public void and_not_when_it_shows_claude_code_got_it() => story.WhenClaudeCodeGotIt.MessageBox.Must().Be("Waiting\nWritten since");
}

public class When_the_ide_restarts_with_a_chat_scrolled_up_and_the_terminal_chosen(When_the_ide_restarts_with_a_chat_scrolled_up_and_the_terminal_chosen.Story story)
   : IClassFixture<When_the_ide_restarts_with_a_chat_scrolled_up_and_the_terminal_chosen.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen Before;
      public readonly ChatSeen After;
      public readonly ChatSeen ShownAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.Give(Conversation());
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.OpenTheFold(1);
         chat.ScrollTheTranscriptTo("a20");
         Before = chat.Switch();
         view.RestartTheIde(tabsToComeBack: 1);
         chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.Give(Conversation());
         Thread.Sleep(500); // Time for the tab to show the chat, were it to.
         After = chat.Seen;
         chat.Switch();
         ShownAfter = chat.SeenOnce(it => it.ItemAtTheTop == "a20", "the conversation to be scrolled to where it was");
      }

      /// <summary>A conversation longer than the pane, each turn with a tool call.</summary>
      static object[] Conversation() =>
      [
         .. Enumerable.Range(1, 30).SelectMany(turn => new[]
         {
            Item.Prompt($"p{turn}", $"Prompt {turn}"),
            Item.ToolCall($"t{turn}", "Bash", new Dictionary<string, string> { ["command"] = $"echo {turn}" }, $"{turn}"),
            Item.AssistantText($"a{turn}", $"Answer {turn}\nover two lines.")
         })
      ];
   }

   [XF] public void before_it_was_scrolled_to_the_item() => story.Before.ItemAtTheTop.Must().Be("a20");
   [XF] public void the_terminal_the_user_chose_is_shown_again() => (story.Before.Card, story.After.Card).Must().Be(("TERMINAL", "TERMINAL"));
   [XF] public void as_the_users_choice() => story.After.CardTheUserChose.Must().Be("TERMINAL");
   [XF] public void the_chat_shows_the_conversation_scrolled_to_where_it_was() => story.ShownAfter.ItemAtTheTop.Must().Be("a20");
   [XF] public void with_the_fold_the_user_opened_open() => story.ShownAfter.Folds.Select(it => it.Open).Take(3).Must().Satisfy(it => it.SequenceEqual([false, true, false]));
}
