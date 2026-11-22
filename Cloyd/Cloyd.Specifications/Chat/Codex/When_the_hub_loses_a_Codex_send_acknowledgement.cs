using Avalonia.Controls;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Avalonia.Input;
using Cloyd.Chat.Writing;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Pasting;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>The hub stops answering while a steer is on the wire. Magnus types again before the failure reaches
/// the chat. Both messages remain his to send and the chat never retries the first in another session.</summary>
public class When_the_hub_loses_a_Codex_send_acknowledgement : SpecificationOpeningWindows
{
   static readonly CodexSessionId FirstSession = new("first-session");
   static readonly CodexTurnId FirstTurn = new("first-turn");
   readonly CodexChatComposer _box;
   readonly HubThatLosesTheAcknowledgement _hub;

   public When_the_hub_loses_a_Codex_send_acknowledgement() => (_box, _hub) = Once(() =>
   {
      var hub = new HubThatLosesTheAcknowledgement();
      var window = Open(() => new Window { Content = new CodexChatComposer(hub, new PastedImages(new DirectoryInfo(Path.GetTempPath())), new CodexDraftsOfATab()) });
      var box = OnTheUIThread(() => (CodexChatComposer)window.Content!);
      OnTheUIThread(() =>
      {
         box.Target(FirstSession, true, FirstTurn);
         box.TakeTheKeyboard();
      });
      Type(window, "the steer");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
      OnTheUIThread(() => box.Target(new CodexSessionId("different-session"), true, null));
      Type(window, "draft for the other session");
      hub.LoseTheAcknowledgement();
      OnTheUIThread(() => box.Target(FirstSession, true, FirstTurn));
      WaitUntil(() => box.Notice?.Contains("unconfirmed") == true, "the editor reported the unknown delivery");
      return (box, hub);
   });

   [XF] public void the_command_named_the_session_and_turn_that_were_on_screen_when_sent() =>
      _hub.Submissions.Must().SequenceEqual([(FirstSession, FirstTurn, "the steer")]);

   [XF] public void the_unconfirmed_message_returns_to_its_original_sessions_editor() =>
      OnTheUIThread(() => _box.Draft.Document.Text).Must().Be("the steer");

   [XF] public void the_other_session_keeps_its_own_draft()
   {
      OnTheUIThread(() => _box.Target(new CodexSessionId("different-session"), true, null));
      OnTheUIThread(() => _box.Draft.Document.Text).Must().Be("draft for the other session");
      OnTheUIThread(() => _box.Target(FirstSession, true, FirstTurn));
   }

   sealed class HubThatLosesTheAcknowledgement : ICodexChatInTheHub
   {
      readonly TaskCompletionSource _acknowledgement = new(TaskCreationOptions.RunContinuationsAsynchronously);
      public List<(CodexSessionId, CodexTurnId?, string)> Submissions { get; } = [];

      public Task Send(CodexSessionId session, MessageFromTheChat message) => throw new NotSupportedException();
      public Task Steer(CodexSessionId session, SubmittedCodexSteer steer)
      {
         Submissions.Add((session, steer.Turn, steer.Message.AsShownInTheChat));
         return _acknowledgement.Task;
      }

      public void LoseTheAcknowledgement() => _acknowledgement.SetException(new HttpRequestException("The connection closed without a response."));

      public IAsyncEnumerable<CodexChatReading> Follow(CodexSessionId session, CancellationToken cancellation) => throw new NotSupportedException();
      public Task<CodexChatReading> SavedConversation(CodexSessionId session, CancellationToken cancellation) => throw new NotSupportedException();
      public Task<CodexChatBackgroundTerminals?> BackgroundTerminals(CodexSessionId session, CancellationToken cancellation) => throw new NotSupportedException();
      public Task Interrupt(CodexSessionId session, CodexTurnId turn) => throw new NotSupportedException();
   }
}
