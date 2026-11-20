using System.Text.RegularExpressions;
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
      public readonly string? TheShortcutInTheChat;
      public readonly string? TheShortcutInTheTerminal;
      public readonly string? TheShortcutElsewhere;

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
         TheShortcutInTheChat = chat.TheActionTheShortcutRuns(SwitchShortcut, "chat");
         TheShortcutInTheTerminal = chat.TheActionTheShortcutRuns(SwitchShortcut, "terminal");
         TheShortcutElsewhere = chat.TheActionTheShortcutRuns(SwitchShortcut, "elsewhere");
      }
   }

   const string SwitchShortcut = "ctrl H";
   const string SwitchAction = "ClaudeSessions.SwitchBetweenTheChatAndTheTerminal";

   [XF] public void it_shows_its_terminal_until_claude_code_runs() => story.BeforeClaudeCodeRuns.Card.Must().Be("TERMINAL");
   [XF] public void then_its_chat() => story.OnceClaudeCodeRuns.Card.Must().Be("CHAT");
   [XF] public void each_under_a_tab_of_its_own() => story.OnceClaudeCodeRuns.Tabs.Must().Satisfy(it => it.SequenceEqual(["Chat", "Terminal"]));
   [XF] public void the_status_line_says_claude_is_idle() => story.OnceClaudeCodeRuns.StatusLine.Must().Be("Claude is idle");

   [XF] public void ctrl_h_is_the_switchs_shortcut_in_the_default_keymap() => story.TheSwitchsShortcutInTheDefaultKeymap.Must().Satisfy(it => it.Contains(SwitchAction));
   [XF] public void and_in_visual_studio_2022s() => story.TheSwitchsShortcutInVisualStudio2022.Must().Satisfy(it => it.Contains(SwitchAction));
   [XF] public void pressed_in_the_chat_it_switches() => story.TheShortcutInTheChat.Must().Be(SwitchAction);
   [XF] public void and_in_the_terminal() => story.TheShortcutInTheTerminal.Must().Be(SwitchAction);
   [XF] public void but_not_elsewhere_in_the_ide() => story.TheShortcutElsewhere.Must().Satisfy(it => it != SwitchAction);
}

public class When_the_user_clicks_the_tab_of_a_card(When_the_user_clicks_the_tab_of_a_card.Story story) : IClassFixture<When_the_user_clicks_the_tab_of_a_card.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen AfterTheTerminalsTab;
      public readonly ChatSeen ThroughADialog;
      public readonly ChatSeen AfterTheChatsTab;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         AfterTheTerminalsTab = chat.ClickTheTabOf("TERMINAL");
         chat.SayTheStatus("WAITING");
         chat.SayTheStatus("IDLE");
         Thread.Sleep(500);
         ThroughADialog = chat.Seen;
         AfterTheChatsTab = chat.ClickTheTabOf("CHAT");
      }
   }

   [XF] public void the_terminals_tab_shows_the_terminal() => story.AfterTheTerminalsTab.Card.Must().Be("TERMINAL");
   [XF] public void as_the_users_choice() => story.AfterTheTerminalsTab.CardTheUserChose.Must().Be("TERMINAL");
   [XF] public void which_stays_through_a_dialog() => story.ThroughADialog.Card.Must().Be("TERMINAL");
   [XF] public void the_chats_tab_shows_the_chat() => story.AfterTheChatsTab.Card.Must().Be("CHAT");
}

public class When_the_chat_shows_a_conversation(When_the_chat_shows_a_conversation.Story story) : IClassFixture<When_the_chat_shows_a_conversation.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen Shown;
      public readonly ChatSeen WithTheRunOpen;
      public readonly ChatSeen OnceTheResultCame;
      public readonly ChatSeen WithLinks;
      public readonly ChatSeen AfterARewind;
      public readonly ChatSeen WithTheMessageOpen;
      public readonly ChatSeen OnceClaudeIsIdle;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("BUSY");
         object[] conversation =
         [
            Item.Prompt("p1", "Fix the bug in the circle"),
            Item.FromAnotherSession("m1", "tooling-owner", "Yes, through the hub.\nThe shape I'm having built now:"),
            Item.FromABackgroundTask("b1", "<task-notification>\n<task-id>aba631c</task-id>\n<status>completed</status>\n<summary>Agent \"Fix the hub\" finished</summary>\n<result>Fixed.</result>\n</task-notification>"),
            Item.Thinking("k1", "The area uses the diameter."),
            Item.AssistantText("a1", "## Looking at it\n\nThe area is **wrong**:\n\n- it uses `diameter`"),
            Item.ToolCall("t1", "Bash", new Dictionary<string, string> { ["command"] = "git status", ["description"] = "Show the working tree" }, "On branch main"),
            Item.AssistantText("a2", "Fixed **Circle.Area** in Shapes/Circle.cs:7."),
            Item.Compaction("c1"),
            Item.Prompt("p2", "/compact"),
            Item.Prompt("p3", "Run the tests"),
            Item.ToolCall("t2", "Read", new Dictionary<string, string> { ["file_path"] = @"C:\Some\File.cs" }, "1  ```code with fences```"),
            Item.ToolCall("t3", "Bash", new Dictionary<string, string> { ["command"] = "dotnet test" }, result: null)
         ];
         Shown = chat.Give(conversation);
         chat.ClickTheRow(Run);
         WithTheRunOpen = chat.ClickTheRow("t3");
         conversation[^1] = Item.ToolCall("t3", "Bash", new Dictionary<string, string> { ["command"] = "dotnet test" }, "Failed: 1 in Shapes/Circle.cs:7", isAnError: true);
         chat.Give(conversation);
         OnceTheResultCame = chat.SeenOnce(it => it.Row("t3").Body!.Contains("Failed: 1"), "the tool call's result to show");
         WithLinks = chat.SeenOnce(it => it.Links.Count(link => link == "Shapes/Circle.cs:7") == 2, "the symbol linker's links to show");
         chat.Give([.. conversation[..^2], Item.AssistantText("a3", "Went another way.")]); // As a rewind does.
         AfterARewind = chat.SeenOnce(it => it.Transcript.Contains("Went another way."), "the conversation to go another way");
         WithTheMessageOpen = chat.ClickTheRow("m1");
         chat.SayTheStatus("IDLE");
         OnceClaudeIsIdle = chat.SeenOnce(it => it.StatusLine == "Claude is idle", "the chat to show that Claude is idle");
      }
   }

   /// <summary>The key of the row of the run of tool calls t2 and t3.</summary>
   const string Run = "tool calls from t2";

   [XF] public void a_prompt_shows_as_the_users() => story.Shown.Rows[0].Must().Be(new RowSeen("p1", "prompt", "Fix the bug in the circle", null, null, false, false));

   [XF] public void another_sessions_message_shows_as_one_line_with_whose_it_is_and_its_first_line() =>
      (story.Shown.Row("m1").Kind, story.Shown.Row("m1").Text, story.Shown.Row("m1").Open).Must().Be(("from", "From tooling-owner · Yes, through the hub.", false));

   [XF] public void a_background_tasks_notice_as_one_line_with_its_summary() =>
      (story.Shown.Row("b1").Text, story.Shown.Row("b1").Open).Must().Be(("A background task finished · Agent \"Fix the hub\" finished", false));

   [XF] public void opened_the_message_shows_whole_below_its_line() =>
      (story.WithTheMessageOpen.Row("m1").Open, story.WithTheMessageOpen.Row("m1").Body).Must().Be((true, "Yes, through the hub.\nThe shape I'm having built now:"));

   [XF] public void claudes_thinking_shows_too() => (story.Shown.Row("k1").Kind, story.Shown.Row("k1").Text).Must().Be(("thinking", "The area uses the diameter."));
   [XF] public void what_claude_said_shows_as_its_markdown_renders() => story.Shown.Row("a1").Text.Must().Be("Looking at it\nThe area is wrong:\nit uses diameter");

   [XF] public void a_tool_call_by_itself_shows_as_its_header() => (story.Shown.Row("t1").Kind, story.Shown.Row("t1").Text).Must().Be(("tool-call", "Bash: git status"));
   [XF] public void with_its_input_and_result_in_its_body() => story.Shown.Row("t1").Body.Must().Be("command: git status\ndescription: Show the working tree\n── result ──\nOn branch main");
   [XF] public void collapsed() => story.Shown.Row("t1").Open.Must().BeFalse();

   [XF] public void tool_calls_made_one_after_the_other_show_as_one_line_that_counts_them() =>
      (story.Shown.Row(Run).Kind, story.Shown.Row(Run).Text).Must().Be(("tool-calls", "Read 1 file, ran 1 command · running"));

   [XF] public void collapsed_too_with_no_rows_of_their_own() => (story.Shown.Row(Run).Open, story.Shown.Rows.Any(it => it.Key is "t2" or "t3")).Must().Be((false, false));
   [XF] public void a_compaction_shows_as_a_divider() => (story.Shown.Row("c1").Kind, story.Shown.Row("c1").Text).Must().Be(("compaction", "The conversation was compacted here"));
   [XF] public void the_status_line_says_claude_is_busy() => story.Shown.StatusLine.Must().Be("Claude is busy");

   [XF] public void the_working_line_says_which_tool_call_runs_how_long_the_turn_has_taken_and_counts_its_commands() =>
      story.Shown.WorkingLine.Must().Satisfy(it => it is not null && Regex.IsMatch(it, @"^Running Bash: dotnet test · \d+s · 2 commands$"));

   [XF] public void once_the_call_has_its_result_it_says_claude_works() =>
      story.OnceTheResultCame.WorkingLine.Must().Satisfy(it => it is not null && Regex.IsMatch(it, @"^Working… · \d+s · 2 commands$"));

   [XF] public void and_shows_no_more_once_claude_is_idle() => story.OnceClaudeIsIdle.WorkingLine.Must().BeNull();

   [XF] public void opened_the_run_shows_its_calls_below_it() =>
      story.WithTheRunOpen.Rows.Select(it => (it.Key, it.Text, it.InARun)).TakeLast(2).Must().Satisfy(it => it.SequenceEqual([("t2", @"Read: C:\Some\File.cs", true), ("t3", "Bash: dotnet test · running", true)]));

   [XF] public void a_call_without_a_result_shows_it_runs() => story.WithTheRunOpen.Row("t3").Body.Must().EndWith("── running ──");

   [XF] public void when_a_tool_calls_result_comes_it_shows_in_the_body() => story.OnceTheResultCame.Row("t3").Body.Must().EndWith("── failed ──\nFailed: 1 in Shapes/Circle.cs:7");
   [XF] public void and_the_calls_line_and_its_runs_say_it_failed() => (story.OnceTheResultCame.Row("t3").Text, story.OnceTheResultCame.Row(Run).Text).Must().Be(("Bash: dotnet test · failed", "Read 1 file, ran 1 command · 1 failed"));
   [XF] public void what_the_user_opened_stays_open() => (story.OnceTheResultCame.Row(Run).Open, story.OnceTheResultCame.Row("t3").Open).Must().Be((true, true));
   [XF] public void and_what_came_before_it_stays_as_it_was() => story.OnceTheResultCame.Rows.Take(8).Must().Satisfy(it => it.SequenceEqual(story.Shown.Rows.Take(8)));

   [XF] public void the_console_filters_link_what_they_link_in_a_terminal() => story.WithLinks.Links.Must().Contain("Circle.Area").Contain("Shapes/Circle.cs:7");

   [XF] public void when_the_conversation_goes_another_way_what_it_left_goes() =>
      story.AfterARewind.Rows.Select(it => it.Key).Must().Satisfy(it => it.SequenceEqual(["p1", "m1", "b1", "k1", "a1", "t1", "a2", "c1", "p2", "p3", "a3"]));

   [XF] public void with_its_links() => story.AfterARewind.Links.Must().Satisfy(it => it.SequenceEqual(["Circle.Area", "Shapes/Circle.cs:7"]));
}

public class When_claude_code_tells_what_it_shows_under_its_box_and_what_its_turn_does(When_claude_code_tells_what_it_shows_under_its_box_and_what_its_turn_does.Story story)
   : IClassFixture<When_claude_code_tells_what_it_shows_under_its_box_and_what_its_turn_does.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen Thinking;
      public readonly ChatSeen WritingAToolCall;
      public readonly ChatSeen RunningTools;
      public readonly ChatSeen InAutoModeOnAModelWithoutEffort;
      public readonly ChatSeen WithNoStatusLineToldOf;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.Give(Item.Prompt("p1", "Write the file and test it"));
         chat.SayTheStatus("BUSY");
         var opusInBypass = new { model = new { id = "claude-opus-5-5", displayName = "Opus 5.5" }, effortLevel = "xhigh", contextWindow = new { usedPercentage = 31, size = 1000000 }, permissionMode = new { name = "bypassPermissions", shownAs = "bypass permissions on" } };
         var startedAt = DateTime.UtcNow.AddSeconds(-75).ToString("O");

         chat.SayWhatClaudeCodeShows(opusInBypass, Turn("thinking", toolName: null, outputTokensSoFar: 228));
         Thinking = chat.SeenOnce(it => it.WorkingLine?.StartsWith("Thinking") == true && it.ClaudeCodesStatusLine is not null, "the chat to show that Claude thinks");

         chat.SayWhatClaudeCodeShows(opusInBypass, Turn("writingAToolCall", toolName: "Write", outputTokensSoFar: 2044));
         WritingAToolCall = chat.SeenOnce(it => it.WorkingLine?.StartsWith("Writing") == true, "the chat to show that Claude writes a tool call");

         chat.Give(Item.Prompt("p1", "Write the file and test it"), Item.ToolCall("t1", "Bash", new Dictionary<string, string> { ["command"] = "dotnet test" }, result: null));
         chat.SayWhatClaudeCodeShows(opusInBypass, Turn("runningTools", toolName: null, outputTokensSoFar: 2044));
         RunningTools = chat.SeenOnce(it => it.WorkingLine?.StartsWith("Running") == true, "the chat to show that Claude runs a tool call");

         var opusInAuto = new { model = new { id = "claude-opus-5-5", displayName = "Opus 5.5" }, effortLevel = (string?)null, contextWindow = new { usedPercentage = (int?)null, size = 1000000 }, permissionMode = new { name = "auto", shownAs = "auto mode on" } };
         chat.SayWhatClaudeCodeShows(opusInAuto, Turn("runningTools", toolName: null, outputTokensSoFar: 2044));
         InAutoModeOnAModelWithoutEffort = chat.SeenOnce(it => it.ClaudeCodesStatusLine?.Contains("Auto") == true, "the chat to show the auto mode");

         chat.SayWhatClaudeCodeShows(statusLine: null, turnUnderWay: null);
         WithNoStatusLineToldOf = chat.SeenOnce(it => it.ClaudeCodesStatusLine is null, "the chat to show no status line of Claude Code's");

         object Turn(string kind, string? toolName, int outputTokensSoFar) =>
            new { turnId = "e1f0a8df-3c2c-4571-8f70-d3d390f1cd3b", startedAt, activity = new { kind, since = startedAt, toolUseId = toolName is null ? null : "toolu_01", toolName }, outputTokensSoFar };
      }
   }

   [XF] public void the_line_under_the_box_shows_claude_codes_model_effort_permission_mode_and_context_used_at_its_right() =>
      story.Thinking.ClaudeCodesStatusLine.Must().Be("Opus 5.5 · xhigh · Bypass permissions · context 31%");

   [XF] public void the_working_line_says_claude_thinks_how_long_the_turn_has_taken_since_it_started_and_its_output_tokens() =>
      story.Thinking.WorkingLine.Must().Satisfy(it => it is not null && Regex.IsMatch(it, @"^Thinking… · 1m \d\ds · ↓ 228 tokens$"));

   [XF] public void while_claude_writes_a_tool_call_it_says_which_tool_with_the_tokens_in_thousands() =>
      story.WritingAToolCall.WorkingLine.Must().Satisfy(it => it is not null && Regex.IsMatch(it, @"^Writing the Write call… · 1m \d\ds · ↓ 2\.0k tokens$"));

   [XF] public void while_tools_run_it_says_which_call_runs_and_counts_the_commands() =>
      story.RunningTools.WorkingLine.Must().Satisfy(it => it is not null && Regex.IsMatch(it, @"^Running Bash: dotnet test · 1m \d\ds · ↓ 2\.0k tokens · 1 command$"));

   [XF] public void what_claude_code_doesnt_tell_is_left_out() => story.InAutoModeOnAModelWithoutEffort.ClaudeCodesStatusLine.Must().Be("Opus 5.5 · Auto");

   [XF] public void with_no_status_line_told_of_none_shows() => story.WithNoStatusLineToldOf.StatusLine.Must().Be("Claude is busy");

   [XF] public void and_without_a_turn_told_of_the_working_line_counts_from_when_claude_turned_busy() =>
      story.WithNoStatusLineToldOf.WorkingLine.Must().Satisfy(it => it is not null && Regex.IsMatch(it, @"^Running Bash: dotnet test · \d+s · 1 command$"));
}

public class When_claude_writes_what_isnt_in_the_conversation_yet(When_claude_writes_what_isnt_in_the_conversation_yet.Story story)
   : IClassFixture<When_claude_writes_what_isnt_in_the_conversation_yet.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen WhileItThinks;
      public readonly ChatSeen WhileItWrites;
      public readonly ChatSeen AsItWritesOn;
      public readonly ChatSeen OnceTheConversationHasIt;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.Give(Item.Prompt("p1", "Fix the bug in the circle"));
         chat.SayTheStatus("BUSY");

         chat.SayWhatClaudeCodeShows(null, null, Item.BeingWritten("being-written:t1:0:0", "The area uses", thinking: true));
         WhileItThinks = chat.SeenOnce(it => it.Rows.Count == 2, "the chat to show the thinking being written");

         chat.SayWhatClaudeCodeShows(null, null, Item.BeingWritten("being-written:t1:0:1", "## Looking at it\n\nThe area is **wro"));
         WhileItWrites = chat.SeenOnce(it => it.Rows[^1].Key == "being-written:t1:0:1", "the chat to show the text being written");

         chat.SayWhatClaudeCodeShows(null, null, Item.BeingWritten("being-written:t1:0:1", "## Looking at it\n\nThe area is **wrong**: it uses `diameter`"));
         AsItWritesOn = chat.SeenOnce(it => it.Rows[^1].Text.EndsWith("diameter"), "the chat to show more of the text being written");

         chat.Give(Item.Prompt("p1", "Fix the bug in the circle"), Item.Thinking("k1", "The area uses the diameter."), Item.AssistantText("a1", "## Looking at it\n\nThe area is **wrong**: it uses `diameter`"));
         chat.SayWhatClaudeCodeShows(null, null, blockBeingWritten: null);
         OnceTheConversationHasIt = chat.SeenOnce(it => it.Rows.All(row => !row.Key.StartsWith("being-written")) && it.Rows.Count == 3, "the chat to show the conversation's own");
      }
   }

   [XF] public void thinking_being_written_shows_at_the_conversations_end_as_claudes_thinking() =>
      (story.WhileItThinks.Rows[^1].Kind, story.WhileItThinks.Rows[^1].Text).Must().Be(("thinking", "The area uses"));

   [XF] public void text_being_written_shows_in_its_place_rendered_as_what_claude_said() =>
      (story.WhileItWrites.Rows.Count, story.WhileItWrites.Rows[^1].Kind, story.WhileItWrites.Rows[^1].Text).Must().Be((2, "claude", "Looking at it\nThe area is **wro"));

   [XF] public void and_grows_as_it_streams() => story.AsItWritesOn.Rows[^1].Text.Must().Be("Looking at it\nThe area is wrong: it uses diameter");

   [XF] public void once_the_conversation_has_it_the_conversations_own_shows_in_its_place() =>
      story.OnceTheConversationHasIt.Rows.Select(it => it.Key).Must().Satisfy(it => it.SequenceEqual(["p1", "k1", "a1"]));
}

public class When_claude_makes_another_tool_call_after_one_the_user_opened(When_claude_makes_another_tool_call_after_one_the_user_opened.Story story)
   : IClassFixture<When_claude_makes_another_tool_call_after_one_the_user_opened.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen OnceTheSecondCame;
      public readonly ChatSeen OnceTheThirdCame;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("BUSY");
         chat.Give(Item.Prompt("p1", "Look around"), Call("t1", "dir", "a.txt"));
         chat.ClickTheRow("t1");
         chat.Give(Item.Prompt("p1", "Look around"), Call("t1", "dir", "a.txt"), Call("t2", "type a.txt", result: null));
         OnceTheSecondCame = chat.SeenOnce(it => it.Rows.Any(row => row.Key == Run), "the run of calls to show");
         chat.ClickTheRow(Run);
         chat.Give(Item.Prompt("p1", "Look around"), Call("t1", "dir", "a.txt"), Call("t2", "type a.txt", "hello"), Call("t3", "dir b", result: null));
         OnceTheThirdCame = chat.SeenOnce(it => it.Row(Run).Text.StartsWith("Ran 3"), "the run to count the third call");
      }

      static object Call(string id, string command, string? result) => Item.ToolCall(id, "Bash", new Dictionary<string, string> { ["command"] = command }, result);
   }

   const string Run = "tool calls from t1";

   [XF] public void the_two_show_as_one_line() => story.OnceTheSecondCame.Row(Run).Text.Must().Be("Ran 2 commands · running");
   [XF] public void which_is_open_as_the_call_the_user_opened_was() => story.OnceTheSecondCame.Row(Run).Open.Must().BeTrue();
   [XF] public void with_that_call_still_open_below_it() => story.OnceTheSecondCame.Rows.Select(it => (it.Key, it.Open)).Skip(2).Must().Satisfy(it => it.SequenceEqual([("t1", true), ("t2", false)]));
   [XF] public void a_run_the_user_closed_stays_closed_as_it_grows() => story.OnceTheThirdCame.Row(Run).Open.Must().BeFalse();
}

public class When_the_user_seeks_a_text_in_the_transcript(When_the_user_seeks_a_text_in_the_transcript.Story story)
   : IClassFixture<When_the_user_seeks_a_text_in_the_transcript.Story>
{
   public sealed class Story
   {
      public readonly string? CtrlFInTheTranscript;
      public readonly string? F3BeforeItSeeks;
      public readonly ChatSeen Seeking;
      public readonly string? F3WhileItSeeks;
      public readonly string? ShiftF3WhileItSeeks;
      public readonly ChatSeen AtTheSecond;
      public readonly ChatSeen AtTheThird;
      public readonly ChatSeen AtTheFourth;
      public readonly ChatSeen AtTheFifth;
      public readonly ChatSeen PastTheLast;
      public readonly ChatSeen BackBeforeTheFirst;
      public readonly ChatSeen SeekingWhatIsntThere;
      public readonly ChatSeen Closed;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.Give(Item.Prompt("p1", "Find the needle"),
                   Item.AssistantText("a1", "No **needle** here."),
                   Call("t1", "echo hay", "hay"),
                   Call("t2", "echo more", "a NEEDLE in the hay"),
                   Item.AssistantText("a2", "That was the last needle."),
                   Item.FromAnotherSession("m1", "tooling-owner", "Look again:\nthere is a needle here too."));
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         CtrlFInTheTranscript = chat.TheActionTheShortcutRuns("ctrl F", "transcript");
         F3BeforeItSeeks = chat.TheActionTheShortcutRuns("F3", "transcript");
         Seeking = chat.Find("needle");
         F3WhileItSeeks = chat.TheActionTheShortcutRuns("F3", "transcript");
         ShiftF3WhileItSeeks = chat.TheActionTheShortcutRuns("shift F3", "transcript");
         AtTheSecond = chat.FindNext();
         AtTheThird = chat.FindNext();
         AtTheFourth = chat.FindNext();
         AtTheFifth = chat.FindNext();
         PastTheLast = chat.FindNext();
         BackBeforeTheFirst = chat.FindPrevious();
         SeekingWhatIsntThere = chat.Find("haystack");
         Closed = chat.CloseTheFind();
      }

      static object Call(string id, string command, string result) => Item.ToolCall(id, "Bash", new Dictionary<string, string> { ["command"] = command }, result);
   }

   [XF] public void ctrl_f_in_the_transcript_opens_its_find_bar() => story.CtrlFInTheTranscript.Must().Be("ClaudeSessions.FindInTheTranscript");
   [XF] public void f3_does_nothing_there_until_it_seeks() => story.F3BeforeItSeeks.Must().Satisfy(it => it != "ClaudeSessions.FindNextInTheTranscript");
   [XF] public void then_it_goes_to_the_next_place() => story.F3WhileItSeeks.Must().Be("ClaudeSessions.FindNextInTheTranscript");
   [XF] public void and_shift_f3_to_the_one_before() => story.ShiftF3WhileItSeeks.Must().Be("ClaudeSessions.FindPreviousInTheTranscript");

   [XF] public void the_bar_shows_what_it_seeks() => (story.Seeking.Find!.Shown, story.Seeking.Find.Text).Must().Be((true, "needle"));
   [XF] public void it_counts_every_place_case_aside_collapsed_or_not_and_starts_at_the_first() => story.Seeking.Find!.Count.Must().Be("1/5");
   [XF] public void the_first_is_in_what_the_user_said() => story.Seeking.Find!.CurrentItem.Must().Be("p1");
   [XF] public void the_next_in_what_claude_said_once_rendered() => (story.AtTheSecond.Find!.Count, story.AtTheSecond.Find.CurrentItem).Must().Be(("2/5", "a1"));
   [XF] public void the_third_in_a_tool_calls_body() => (story.AtTheThird.Find!.CurrentItem, story.AtTheThird.Find.CurrentPart).Must().Be(("t2", (int?)1));
   [XF] public void which_it_opens_with_the_run_it_is_in() => (story.AtTheThird.Row("tool calls from t1").Open, story.AtTheThird.Row("t2").Open).Must().Be((true, true));
   [XF] public void the_fourth_in_what_claude_said_last() => (story.AtTheFourth.Find!.Count, story.AtTheFourth.Find.CurrentItem).Must().Be(("4/5", "a2"));
   [XF] public void the_fifth_in_another_sessions_message() => (story.AtTheFifth.Find!.Count, story.AtTheFifth.Find.CurrentItem).Must().Be(("5/5", "m1"));
   [XF] public void which_it_opens() => (story.AtTheFourth.Row("m1").Open, story.AtTheFifth.Row("m1").Open).Must().Be((false, true));
   [XF] public void past_the_last_it_goes_to_the_first() => story.PastTheLast.Find!.Count.Must().Be("1/5");
   [XF] public void and_back_before_the_first_to_the_last() => story.BackBeforeTheFirst.Find!.Count.Must().Be("5/5");
   [XF] public void what_isnt_there_it_says_isnt() => (story.SeekingWhatIsntThere.Find!.Count, story.SeekingWhatIsntThere.Find.CurrentItem).Must().Be(("0 results", (string?)null));
   [XF] public void closed_the_bar_hides() => story.Closed.Find!.Shown.Must().BeFalse();
}

public class When_the_user_sends_a_message_from_the_chat(When_the_user_sends_a_message_from_the_chat.Story story) : IClassFixture<When_the_user_sends_a_message_from_the_chat.Story>
{
   public sealed class Story
   {
      public readonly SentToClaudeCode Sent;
      public readonly ChatSeen Before;
      public readonly ChatSeen After;
      public readonly ChatSeen WhileWritten;
      public readonly ChatSeen WhileWrittenOn;
      public readonly ChatSeen WithSixtyLines;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         Before = chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.Type("First line");
         chat.Press("ENTER");
         chat.Type("second line, ");
         chat.Paste("pasted\r\ntext");
         chat.Type(" and ");
         chat.Paste("one");
         chat.Paste("two");
         chat.Type(" ");
         WhileWritten = chat.PasteAnImage();
         chat.Press("SHIFT_ENTER");
         WhileWrittenOn = chat.Type("third line");
         chat.Press("CTRL_ENTER");
         Sent = chat.SentOnce(it => it.Messages.Count > 0, "the message to reach Claude Code");
         After = chat.Seen;
         WithSixtyLines = chat.Type(string.Join("\n", Enumerable.Range(1, 60)));
      }
   }

   [XF] public void the_box_starts_out_one_line_tall() => story.Before.MessageBoxLines.Must().Be(1);
   // The sandbox Rider's tool window is short: two fifths of its chat have room for three lines.
   [XF] public void and_grows_with_each_line_written_while_it_has_room() =>
      (story.WhileWritten.MessageBoxLines, story.WhileWrittenOn.MessageBoxLines).Must().Be((3, Math.Min(4, story.WhileWrittenOn.MessageBoxRoom)));

   [XF] public void up_to_two_fifths_of_the_chat_past_which_it_scrolls() =>
      (story.WithSixtyLines.MessageBoxLines, story.WithSixtyLines.MessageBoxRoom < 60).Must().Be((story.WithSixtyLines.MessageBoxRoom, true));
   [XF] public void and_is_one_line_again_once_sent() => story.After.MessageBoxLines.Must().Be(1);

   [XF] public void enter_starts_a_new_line() => story.WhileWritten.MessageBox.Must().Be("First line\nsecond line, pasted\ntext and onetwo [Image #1]");
   [XF] public void and_so_does_shift_enter() => story.WhileWrittenOn.MessageBox.Must().Be(story.WhileWritten.MessageBox + "\nthird line");
   [XF] public void neither_sends_it() => story.WhileWrittenOn.MessageBox.Must().Satisfy(it => it?.StartsWith("First line") == true);

   [XF] public void ctrl_enter_sends_it_as_one_message() => story.Sent.Messages.Count.Must().Be(1);

   // Two pastes side by side stay two, as in Claude Code's own box.
   [XF] public void each_part_as_it_came_into_the_box_typed_pasted_or_an_image() =>
      story.Sent.Messages[0].Select(it => (it.Kind, it.Text)).Must().Satisfy(it => it.SequenceEqual([("typed", "First line\nsecond line, "), ("pasted", "pasted\ntext"), ("typed", " and "), ("pasted", "one"), ("pasted", "two"), ("typed", " "), ("image", "[Image #1]"), ("typed", "\nthird line")]));

   [XF] public void the_image_as_a_file_of_its_own() => story.Sent.Messages[0].Single(it => it.Kind == "image").Must().Satisfy(it => it.FileExists && it.File!.EndsWith(".png"));
   [XF] public void the_box_is_empty_after() => story.After.MessageBox.Must().Be("");
   [XF] public void the_chat_stays_shown() => story.After.Card.Must().Be("CHAT");
}

public class When_the_user_pastes_images_into_the_chat(When_the_user_pastes_images_into_the_chat.Story story) : IClassFixture<When_the_user_pastes_images_into_the_chat.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen Before;
      public readonly ChatSeen WithTwo;
      public readonly string? OpenedByAClick;
      public readonly ChatSeen WithTheSecondsPlaceHolderDeleted;
      public readonly ChatSeen AfterSending;
      public readonly SentToClaudeCode Sent;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         Before = chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.PasteAnImage();
         chat.Type(" and ");
         WithTwo = chat.PasteAnImage();
         OpenedByAClick = chat.ClickTheThumbnail(1);
         chat.Type(" end");
         foreach(var _ in Enumerable.Range(0, " end".Length + "[Image #2]".Length)) chat.Press("BACKSPACE");
         WithTheSecondsPlaceHolderDeleted = chat.Seen;
         chat.Press("CTRL_ENTER");
         Sent = chat.SentOnce(it => it.Messages.Count > 0, "the message to reach Claude Code");
         AfterSending = chat.Seen;
      }
   }

   [XF] public void no_thumbnail_shows_while_the_box_holds_no_image() => story.Before.Thumbnails.Must().BeEmpty();

   [XF] public void each_image_shows_as_a_thumbnail_over_the_box_captioned_with_its_place_holder() =>
      story.WithTwo.Thumbnails.Select(it => it.Caption).Must().Satisfy(it => it.SequenceEqual(["[Image #1]", "[Image #2]"]));

   [XF] public void of_the_file_the_image_was_saved_to() =>
      story.WithTwo.Thumbnails.Select(it => it.File).Must().Satisfy(it => it.SequenceEqual(story.WithTwo.MessageBoxParts.Where(part => part.Kind == "image").Select(part => part.File!)));

   [XF] public void clicking_one_shows_its_image_in_the_ides_editor() => story.OpenedByAClick.Must().Be(story.WithTwo.Thumbnails[1].File);
   [XF] public void a_thumbnail_goes_with_its_place_holder() => story.WithTheSecondsPlaceHolderDeleted.Thumbnails.Select(it => it.Caption).Must().Satisfy(it => it.SequenceEqual(["[Image #1]"]));
   [XF] public void and_its_image_with_the_message() => story.Sent.Messages.Single().Where(it => it.Kind == "image").Select(it => it.Text).Must().Satisfy(it => it.SequenceEqual(["[Image #1]"]));
   [XF] public void none_shows_once_the_message_is_sent() => story.AfterSending.Thumbnails.Must().BeEmpty();
}

public class When_the_user_clicks_the_send_button(When_the_user_clicks_the_send_button.Story story) : IClassFixture<When_the_user_clicks_the_send_button.Story>
{
   public sealed class Story
   {
      public readonly ChatSeen WhileTheBoxIsEmpty;
      public readonly ChatSeen WithAMessage;
      public readonly ChatSeen AfterTheClick;
      public readonly ChatSeen WhileClaudeIsBusy;
      public readonly ChatSeen WhileClaudeIsBusyWithAMessage;
      public readonly SentToClaudeCode Sent;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         WhileTheBoxIsEmpty = chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         WithAMessage = chat.Type("Hello");
         AfterTheClick = chat.ClickTheSendButton();
         chat.SentOnce(it => it.Messages.Count > 0, "the message to reach Claude Code");
         chat.SayTheStatus("BUSY");
         WhileClaudeIsBusy = chat.SeenOnce(it => it.StatusLine?.StartsWith("Claude is busy") == true, "the chat to show that Claude is busy");
         chat.ClickTheSendButton();
         Sent = chat.SentOnce(it => it.Interruptions > 0, "the interruption to reach Claude Code");
         WhileClaudeIsBusyWithAMessage = chat.Type("And then");
      }
   }

   [XF] public void it_does_nothing_while_the_box_is_empty() => story.WhileTheBoxIsEmpty.SendButton.Must().Be("NOTHING");
   [XF] public void it_sends_once_the_box_holds_a_message() => story.WithAMessage.SendButton.Must().Be("SEND");
   [XF] public void clicked_it_sends_the_message() => story.Sent.Messages.Single().Must().Satisfy(it => it.SequenceEqual([new MessagePartSeen("typed", "Hello")]));
   [XF] public void and_the_box_is_empty_after() => story.AfterTheClick.MessageBox.Must().Be("");
   [XF] public void while_claude_is_busy_and_the_box_empty_it_stops_claude() => story.WhileClaudeIsBusy.SendButton.Must().Be("STOP");
   [XF] public void clicked_it_interrupts_claude_code() => story.Sent.Interruptions.Must().Be(1);
   [XF] public void with_a_message_in_the_box_it_sends_it_while_claude_is_busy_too() => story.WhileClaudeIsBusyWithAMessage.SendButton.Must().Be("SEND");
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
         AfterTheCommand = chat.Press("CTRL_ENTER");
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
         chat.Press("CTRL_ENTER");
         AfterSending = chat.SeenOnce(it => it.WhyNotTyped is not null, "the chat to say why Claude Code didn't get it");
         Sent = chat.Sent;
         chat.HaveClaudeCodeGetIt();
         chat.Press("CTRL_ENTER");
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
         chat.Press("CTRL_ENTER");
         AfterARefusal = chat.SeenOnce(it => it.Card == "TERMINAL", "the tab to show the terminal");
         BackInTheChat = chat.Switch();
         chat.HaveClaudeCodeLeaveItUnsent(WhyLeftUnsent);
         chat.Press("CTRL_ENTER");
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
         chat.Press("CTRL_ENTER");
         chat.Type("Second");
         chat.Press("CTRL_ENTER");
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
   [XF] public void the_switch_shows_the_other_card() => story.TheTerminalTheUserChose.Card.Must().Be("TERMINAL");
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
         chat.Press("CTRL_ENTER");
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
            chat.Press("CTRL_ENTER");
            chat.Type("Waiting");
            chat.Press("CTRL_ENTER");
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
         chat.ClickTheRow("t2");
         chat.ScrollTheTranscriptTo("a20");
         chat.SeenOnce(it => it.ItemAtTheTop == "a20", "the conversation to be scrolled to the item");
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
   [XF] public void with_the_tool_call_the_user_opened_open() => new[] { "t1", "t2", "t3" }.Select(it => story.ShownAfter.Row(it).Open).Must().Satisfy(it => it.SequenceEqual([false, true, false]));
}
