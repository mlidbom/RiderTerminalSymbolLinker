using System.Threading.Channels;
using Avalonia.Controls;
using Cloyd.Chat;
using Cloyd.Chat.Writing;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
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

   public When_a_Codex_chat_reopens_during_a_turn() => _chat = Once(() =>
   {
      var hub = new ReadingsOfOneSession();
      var session = new CodexSessionId("session");
      var turn = new CodexTurnId("turn");
      var window = Open(() => new Window { Width = 900, Height = 650, Content = new ChatOfACodexSession(hub, new PastedImages(new DirectoryInfo(Path.GetTempPath())), new CodexDraftsOfATab()) });
      var chat = OnTheUIThread(() => (ChatOfACodexSession)window.Content!);
      OnTheUIThread(() => chat.Show(session, new RunningCodex(session, "The chat", null, new StatusOfACodex("busy"))));
      var prompt = new ShownInTheChat.Prompt("prompt", "Check the result");
      var calls = new CodexToolCalls([new CodexToolCall("call", "Ran a command", new CodexToolCallDetails("Get-Date", "Sunday"))]);
      var firstSubscription = hub.NextSubscription();
      firstSubscription.TryWrite(new CodexChatReading([prompt, calls, new CodexReply("answer", "The re")], turn));
      WaitUntil(() => chat.Shown.LastOrDefault()?.TextShown == "The re" && chat.InputBox.ShowsTheStopButton, "the chat showed the partial answer and Stop");
      OnTheUIThread(() => chat.IsVisible = false);
      OnTheUIThread(() => chat.IsVisible = true);
      var reopened = hub.NextSubscription();
      reopened.TryWrite(new CodexChatReading([prompt, calls, new CodexReply("answer", "The result")], turn));
      reopened.TryWrite(new CodexChatReading([prompt, calls, new CodexReply("answer", "The result is Sunday.")], null));
      WaitUntil(() => chat.Shown.LastOrDefault()?.TextShown == "The result is Sunday.", "the completed answer was visible");
      Render(window, "a-Codex-chat-with-a-command-and-a-reply");
      return chat;
   });

   [XF] public void it_shows_the_prompt_command_and_answer_once_in_order() =>
      OnTheUIThread(() => _chat.Shown.Select(it => it.TextShown).ToList()).Must().SequenceEqual(["Check the result", "Ran a command", "The result is Sunday."]);

   [XF] public void the_stop_button_goes_when_the_turn_ends() => OnTheUIThread(() => _chat.InputBox.ShowsTheStopButton).Must().BeFalse();

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
      public Task Send(CodexSessionId session, MessageFromTheChat message, CodexTurnId? steerInto) => throw new NotSupportedException();
      public Task<CodexChatReading> SavedConversation(CodexSessionId session, CancellationToken cancellation) => throw new NotSupportedException();
      public Task Interrupt(CodexSessionId session, CodexTurnId turn) => throw new NotSupportedException();
   }
}
