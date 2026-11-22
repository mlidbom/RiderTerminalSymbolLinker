using Cloyd.Chat.Writing;
using Cloyd.Hub;
using Cloyd.Saving;

namespace Cloyd.Codex.Chat;

/// <summary>A session's editor and submissions. A steer is retained until Codex records its client
/// message ID. Receipts stay with the originating session when Terminal switches to another.</summary>
sealed class CodexSessionDraft
{
   const string Unconfirmed = "Delivery to Codex is unconfirmed. Check the conversation before sending again.";
   const string Sending = "Sending to Codex.";
   readonly List<SubmittedCodexSteer> _pendingSteers = [];
   MessageFromTheChat? _submission;
   SubmittedCodexSteer? _submittingSteer;
   string? _uneditedReturnedSteerId;
   public DraftOfAMessage Draft { get; } = new();
   public bool IsSending => _submission is not null;
   public bool HasUnconfirmedSteersToCopy => _pendingSteers.Any(it => it.Recovery == SteerRecovery.AvailableToCopy);
   public string? Notice { get; private set; }
   public event Action? Changed;

   public CodexSessionDraft() => Draft.Changed += () => _uneditedReturnedSteerId = null;

   public async Task Send(ICodexChatInTheHub hub, CodexSessionId session, CodexTurnId? steerInto)
   {
      var message = _submission = Draft.TakeTheMessage();
      var steer = _submittingSteer = steerInto is null ? null : SubmittedCodexSteer.For(steerInto, message);
      if(steer is not null) _pendingSteers.Add(steer);
      Tell(Sending);
      try
      {
         if(steer is null) await hub.Send(session, message);
         else await hub.Steer(session, steer);
      }
      catch(RefusedByTheHub refusal)
      {
         PutBackIfPending($"The message is back in the box. {refusal.Message}", deliveryUncertain: false);
      }
      catch(Exception failure)
      {
         PutBackIfPending($"{Unconfirmed} {failure.Message}", deliveryUncertain: true);
      }
      finally
      {
         if(Notice == Sending) Notice = null;
         _submission = null;
         _submittingSteer = null;
         Changed?.Invoke();
      }

      void PutBackIfPending(string notice, bool deliveryUncertain)
      {
         if(steer is null) Draft.PutBackAhead(message.Parts);
         else
         {
            var index = _pendingSteers.FindIndex(it => it.ClientUserMessageId == steer.ClientUserMessageId);
            if(index < 0) return;
            RestoreCopy(index, withdrawIfConfirmed: deliveryUncertain);
            if(!deliveryUncertain) _pendingSteers.RemoveAt(index);
         }
         Notice = notice;
      }
   }

   /// <summary>Matches messages by ID in live updates and reconnect snapshots. Restore omitted steers
   /// in submission order ahead of newer typing, even if the HTTP receipt has yet to arrive.</summary>
   public void Observe(CodexChatReading reading)
   {
      var returned = false;
      var observed = false;
      string? confirmed = null;
      for(var index = _pendingSteers.Count - 1; index >= 0; --index)
      {
         var steer = _pendingSteers[index];
         var turn = reading.Turns.FirstOrDefault(it => it.Id == steer.Turn.Value);
         if(reading.SupportsCodexSteerReceipts && turn?.RecordedClientMessageIds.Contains(steer.ClientUserMessageId) == true)
         {
            if(steer.Recovery == SteerRecovery.CopyReturnedToEditor)
            {
               confirmed = "Codex recorded the previously unconfirmed steer.";
               if(_uneditedReturnedSteerId == steer.ClientUserMessageId) Draft.TakeTheMessage();
               else confirmed += " Check your current draft before sending a copy.";
            }
         }
         else if(reading.SupportsCodexSteerReceipts && turn?.HasEnded == true)
         {
            if(steer.Recovery != SteerRecovery.CopyReturnedToEditor)
            {
               RestoreCopy(index, withdrawIfConfirmed: false);
               returned = true;
            }
            else confirmed = "The turn ended without recording the previously unconfirmed steer. Review the copy already returned to you.";
            if(_uneditedReturnedSteerId == steer.ClientUserMessageId) _uneditedReturnedSteerId = null;
         }
         else
         {
            if((!reading.SupportsCodexSteerReceipts || turn?.IsUnderWay != true) && steer.Recovery == SteerRecovery.AwaitingHistory)
            {
               _pendingSteers[index] = steer with { Recovery = SteerRecovery.AvailableToCopy };
               observed = true;
            }
            else if(reading.SupportsCodexSteerReceipts && turn?.IsUnderWay == true && steer.Recovery == SteerRecovery.AvailableToCopy)
            {
               _pendingSteers[index] = steer with { Recovery = SteerRecovery.AwaitingHistory };
               observed = true;
            }
            continue;
         }
         _pendingSteers.RemoveAt(index);
         observed = true;
      }
      if(returned) Tell("The conversation does not show your steer. It is back in the box. Check it before sending again.");
      else if(confirmed is not null) Tell(confirmed);
      else if(observed && Notice == Unconfirmed && _pendingSteers.Count == 0) Tell(null);
      else if(observed) Changed?.Invoke();
   }

   /// <summary>Gives Magnus a copy when available history cannot settle delivery. Later proof can update the
   /// notice, and only an explicit Send submits the copy.</summary>
   public void CopyUnconfirmedSteersToEditor()
   {
      for(var index = _pendingSteers.Count - 1; index >= 0; --index)
         if(_pendingSteers[index].Recovery == SteerRecovery.AvailableToCopy) RestoreCopy(index, withdrawIfConfirmed: false);
      Tell(Unconfirmed);
   }

   void RestoreCopy(int index, bool withdrawIfConfirmed)
   {
      var steer = _pendingSteers[index];
      if(steer.Recovery == SteerRecovery.CopyReturnedToEditor) return;
      var editorWasEmpty = Draft.Document.TextLength == 0;
      Draft.PutBackAhead(steer.Parts);
      _pendingSteers[index] = steer with { Recovery = SteerRecovery.CopyReturnedToEditor };
      if(withdrawIfConfirmed && editorWasEmpty) _uneditedReturnedSteerId = steer.ClientUserMessageId;
   }

   public void Tell(string? notice)
   {
      Notice = notice;
      Changed?.Invoke();
   }

   public SavedCodexDraft? AsSaved(CodexSessionId? session)
   {
      var parts = _submittingSteer is null && _submission is {} message ? Draft.WithMessageAhead(message) : Draft.Parts;
      var awaitingReceipt = IsSending && (_submittingSteer is null || _pendingSteers.Any(it => it.ClientUserMessageId == _submittingSteer.ClientUserMessageId));
      var notice = awaitingReceipt ? Unconfirmed : Notice == Sending ? null : Notice;
      return parts.Count == 0 && _pendingSteers.Count == 0 ? null :
         new SavedCodexDraft(session?.Value, parts, notice, _pendingSteers.Count == 0 ? null : _pendingSteers.ToArray(), _uneditedReturnedSteerId);
   }

   public void Restore(SavedCodexDraft saved)
   {
      var editorWasEmpty = Draft.Document.TextLength == 0;
      Draft.PutBackAhead(saved.Parts);
      if(saved.PendingSteers is {} steers) _pendingSteers.AddRange(steers);
      _uneditedReturnedSteerId = editorWasEmpty ? saved.UneditedReturnedSteerId : null;
      Tell(saved.Notice);
   }
}
