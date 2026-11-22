using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Cloyd.Chat;
using Cloyd.Chat.Status;
using Cloyd.Chat.Views;
using Cloyd.Codex.Chat.Status;
using Cloyd.Hub.RunningSessions;
using Cloyd.Pasting;
using static Cloyd.Chat.Views.LinesOfTheChat;

namespace Cloyd.Codex.Chat;

/// <summary>The conversation of the Codex session the terminal in this tab shows. Opening it follows the same
/// session from the hub; the terminal remains the place to answer approvals and other interactive questions.</summary>
sealed class ChatOfACodexSession : SessionChat
{
   readonly ICodexChatInTheHub _hub;
   OpenedInTheChat _opened = new();
   readonly TextBlock _status = new() { MaxWidth = WidthOfTheColumn, Margin = new Thickness(16, 4), IsVisible = false };
   CodexSessionId? _session;
   RunningCodex? _running;
   CodexChatReading? _reading;
   CancellationTokenSource? _following;

   public ChatOfACodexSession(ICodexChatInTheHub hub, PastedImages pastedImages, CodexDraftsOfATab drafts)
   {
      _hub = hub;
      InputBox = new CodexChatComposer(hub, pastedImages, drafts) { MaxWidth = WidthOfTheColumn + 32 };
      BackgroundTerminals = new BackgroundTerminalsOfACodexChat(hub.BackgroundTerminals) { MaxWidth = WidthOfTheColumn + 32 };
      ColoursOfTheChat.Grey.AsTheForegroundOf(_status);
      ArrangeRows(WaitingForMagnusInTheTerminal, Progress, InputBox, StatusLine, BackgroundTerminals, _status);
   }

   public override CodexChatComposer InputBox { get; }
   public ProgressOfACodexChat Progress { get; } = new() { MaxWidth = WidthOfTheColumn + 32 };
   public StatusLineOfACodexChat StatusLine { get; } = new() { MaxWidth = WidthOfTheColumn + 32 };
   public BackgroundTerminalsOfACodexChat BackgroundTerminals { get; }
   public WaitingForMagnusInTheTerminal WaitingForMagnusInTheTerminal { get; } = new("Codex waits for you in the terminal") { MaxWidth = WidthOfTheColumn + 32 };

   /// <summary>Follows the last session in the tab, including its saved history after exit. Only a running session can take input.</summary>
   public void Show(CodexSessionId? session, RunningCodex? running)
   {
      var wasRunning = _running is not null;
      _running = running;
      if(session != _session)
      {
         _session = session;
         _opened = new OpenedInTheChat();
         Tell(null);
         ShowConversation([]);
         FollowWhileShown();
      }
      else if(wasRunning != (running is not null)) FollowWhileShown();
      UpdateControls();
   }

   protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnAttachedToVisualTree(e);
      FollowWhileShown();
   }

   protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnDetachedFromVisualTree(e);
      StopFollowing();
   }

   protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
   {
      base.OnPropertyChanged(change);
      if(change.Property == IsVisibleProperty && TopLevel.GetTopLevel(this) is not null) FollowWhileShown();
   }

   void StopFollowing()
   {
      _following?.Cancel();
      _following = null;
      _reading = null;
      UpdateControls();
   }

   void FollowWhileShown()
   {
      StopFollowing();
      if(!IsShown || _session is not {} session) return;
      var subscription = _following = new CancellationTokenSource();
      _ = Follow(session, _running is not null, subscription);
   }

   async Task Follow(CodexSessionId session, bool running, CancellationTokenSource subscription)
   {
      try
      {
         await Task.Run(async () =>
         {
            if(running)
               await foreach(var reading in _hub.Follow(session, subscription.Token))
                  await Dispatcher.UIThread.InvokeAsync(() => DrawIfStillFollowing(reading));
            else
            {
               var saved = await _hub.SavedConversation(session, subscription.Token);
               await Dispatcher.UIThread.InvokeAsync(() => DrawIfStillFollowing(saved));
            }
         });
      }
      catch(OperationCanceledException) when(subscription.IsCancellationRequested) {}
      catch(Exception failure)
      {
         if(!subscription.IsCancellationRequested) Tell($"The Codex conversation is unavailable. {failure.Message}");
      }
      finally
      {
         if(_following == subscription)
         {
            _following = null;
            _reading = null;
            UpdateControls();
         }
         subscription.Dispose();
      }

      void DrawIfStillFollowing(CodexChatReading? reading)
      {
         if(subscription.IsCancellationRequested) return;
         var first = Shown.Count == 0;
         _reading = reading;
         UpdateControls();
         if(reading is null)
            Tell("Reconnecting to the Codex conversation…");
         else
         {
            Tell(null);
            ShowConversation(reading.Rows);
            if(first) ScrollToTheEnd();
         }
      }
   }

   void UpdateControls()
   {
      var waiting = _running?.Status.IsWaiting == true;
      WaitingForMagnusInTheTerminal.Show(waiting, _running?.Status.WaitingFor == "approval"
                                                     ? "Review the approval request in the terminal."
                                                     : "Answer the prompt in the terminal.");
      var turn = _reading?.TurnUnderWay;
      Progress.Show(_running is not null && !waiting ? _reading?.Progress : null);
      StatusLine.Show(_running is not null ? _reading?.Footer : null);
      BackgroundTerminals.Observe(_reading?.Footer is not null ? _running?.SessionId : null, turn);
      _status.Text = _running is null && _session is not null ? "Saved conversation · Codex is not running in this tab." : null;
      _status.IsVisible = _status.Text is not null;
      InputBox.Target(_session, _reading is not null && _running?.SessionId == _session, turn);
   }

   protected override Control DrawRow(ShownInTheChat shown) => shown switch
   {
      ShownInTheChat.Prompt prompt => Bubble(Coloured(prompt.Text, ColoursOfTheChat.Text)),
      ShownInTheChat.PeerMessage peer => peer.Draw(_opened.PeerMessages),
      CodexReply reply => MarkdownOf(reply.Markdown),
      CodexToolCalls calls => calls.Draw(_opened),
      CodexChatNotice notice => Grey(notice.Line),
      EndOfACodexTurn end => Grey(end.TextShown),
      _ => throw new ArgumentOutOfRangeException(nameof(shown), shown, "Unknown Codex conversation row.")
   };
}
