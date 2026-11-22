using System.Threading.Channels;
using Avalonia.Controls;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat;
using Cloyd.Chat.Writing;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Codex.Chat.Status;
using Cloyd.Hub.RunningSessions;
using Cloyd.Pasting;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>Opening Chat again during a turn receives a snapshot containing the same item that was streaming
/// when Magnus switched to Terminal. The command and final answer each appear once.</summary>
public class When_a_Codex_chat_reopens_during_a_turn : SpecificationOpeningWindows
{
   readonly ChatOfACodexSession _chat;
   readonly string _callShown;
   readonly string _workingLine;
   readonly string _background;

   public When_a_Codex_chat_reopens_during_a_turn() => (_chat, _callShown, _workingLine, _background) = Once(() =>
   {
      var hub = new ReadingsOfOneSession();
      var session = new CodexSessionId("01a202a3-9da9-7380-96c9-8ec53a92bf48");
      var turn = new CodexTurnProgress(new CodexTurnId("turn"), DateTimeOffset.UtcNow.AddSeconds(-39), "Running tools…", 2,
                                       [new CodexCallUnderWay("Running command", "dotnet test Cloyd/Cloyd.Specifications", DateTimeOffset.UtcNow.AddSeconds(-8))]);
      var footer = new CodexChatFooter(session.Value, ["thread-id", "model-with-reasoning", "current-dir", "thread-name", "context-used", "weekly-limit", "used-tokens", "total-input-tokens", "total-output-tokens"], true)
      {
         Model = "GPT-6.2-Astra", ReasoningEffort = "xhigh", CurrentDirectory = @"C:\Dev\ClaudeSymbolLinker", ThreadName = "codex-codex-chat-ui",
         ContextUsedPercent = 64, WeeklyRemainingPercent = 89, UsedTokens = 1_130_000, TotalInputTokens = 141_000_000, TotalOutputTokens = 172_000,
         PermissionProfile = ":workspace", ApprovalPolicy = "on-request", ApprovalsReviewer = "user"
      };
      var window = Open(() => new Window { Width = 650, Height = 530, Content = new ChatOfACodexSession(hub, new PastedImages(new DirectoryInfo(Path.GetTempPath())), new CodexDraftsOfATab()) });
      var chat = OnTheUIThread(() => (ChatOfACodexSession)window.Content!);
      OnTheUIThread(() => chat.Show(session, new RunningCodex(session, "The chat", null, new StatusOfACodex("busy"))));
      var prompt = new ShownInTheChat.Prompt("prompt", "Check the result");
      var calls = new CodexToolCalls([new CodexToolCall("call", "Ran a command", new CodexToolCallDetails("Get-Date", "Sunday"))]);
      var firstSubscription = hub.NextSubscription();
      firstSubscription.TryWrite(new CodexChatReading([prompt, calls, new CodexReply("answer", "The re")], turn, footer));
      WaitUntil(() => chat.Shown.LastOrDefault()?.TextShown == "The re" && chat.InputBox.ShowsTheStopButton, "the chat showed the partial answer and Stop");
      WaitUntil(() => chat.BackgroundTerminals.Text == "1 background terminal", "the background terminal count arrived");
      OnTheUIThread(() => chat.Progress.CurrentInput).Must().Be("dotnet test Cloyd/Cloyd.Specifications");
      var status = OnTheUIThread(() => (chat.Progress.CurrentCall!, chat.Progress.WorkingLine!, ToolTip.GetTip(chat.BackgroundTerminals) as string));
      Render(window, "a-Codex-chat-with-live-progress-and-configured-footer");
      OnTheUIThread(() => chat.IsVisible = false);
      OnTheUIThread(() => chat.IsVisible = true);
      var reopened = hub.NextSubscription();
      reopened.TryWrite(new CodexChatReading([prompt, calls, new CodexReply("answer", "The result")], turn, footer));
      reopened.TryWrite(new CodexChatReading([prompt, calls, new CodexReply("answer", "The result is Sunday.")], null, footer));
      WaitUntil(() => chat.Shown.LastOrDefault()?.TextShown == "The result is Sunday.", "the completed answer was visible");
      Render(window, "a-Codex-chat-with-a-command-and-a-reply");
      return (chat, status.Item1, status.Item2, status.Item3!);
   });

   [XF] public void it_shows_the_prompt_command_and_answer_once_in_order() =>
      OnTheUIThread(() => _chat.Shown.Select(it => it.TextShown).ToList()).Must().SequenceEqual(["Check the result", "Ran a command", "The result is Sunday."]);

   [XF] public void the_stop_button_goes_when_the_turn_ends() => OnTheUIThread(() => _chat.InputBox.ShowsTheStopButton).Must().BeFalse();

   [XF] public void the_turn_and_its_running_call_have_separate_progress_lines()
   {
      _callShown.Must().Contain("Running command · ");
      _workingLine.Must().Satisfy(it => it.StartsWith("Running tools… (") && it.EndsWith(" · 2 commands)"));
      OnTheUIThread(() => _chat.Progress.IsVisible).Must().BeFalse();
   }

   [XF] public void the_entire_configured_footer_stays_after_the_turn() =>
      OnTheUIThread(() => _chat.StatusLine.TextShown).Must().Be(@"01a202a3-9da9-7380-96c9-8ec53a92bf48 · GPT-6.2-Astra xhigh · C:\Dev\ClaudeSymbolLinker · codex-codex-chat-ui · Context 64% used · weekly 89% left · 1.13M used · 141M in · 172K out");

   [XF] public void it_shows_the_permissions_below_and_the_background_command_on_hover()
   {
      OnTheUIThread(() => _chat.StatusLine.PermissionsShown).Must().Be("Workspace · Ask for approval");
      _background.Must().Be("dotnet watch");
   }

   sealed class ReadingsOfOneSession : ICodexChatInTheHub
   {
      readonly Channel<ChannelWriter<CodexChatReading>> _subscriptions = Channel.CreateUnbounded<ChannelWriter<CodexChatReading>>();
      public IAsyncEnumerable<CodexChatReading> Follow(CodexSessionId session, CancellationToken cancellation)
      {
         var events = Channel.CreateUnbounded<CodexChatReading>();
         _subscriptions.Writer.TryWrite(events.Writer);
         return events.Reader.ReadAllAsync(cancellation);
      }

      public ChannelWriter<CodexChatReading> NextSubscription() =>
         _subscriptions.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
      public Task Send(CodexSessionId session, MessageFromTheChat message) => throw new NotSupportedException();
      public Task Steer(CodexSessionId session, SubmittedCodexSteer steer) => throw new NotSupportedException();
      public Task<CodexChatReading> SavedConversation(CodexSessionId session, CancellationToken cancellation) => throw new NotSupportedException();
      public Task<CodexChatBackgroundTerminals?> BackgroundTerminals(CodexSessionId session, CancellationToken cancellation) =>
         Task.FromResult<CodexChatBackgroundTerminals?>(new([new CodexChatBackgroundTerminal("one", "terminal-1", "dotnet watch", @"C:\Dev\ClaudeSymbolLinker", null)]));
      public Task Interrupt(CodexSessionId session, CodexTurnId turn) => throw new NotSupportedException();
   }
}
