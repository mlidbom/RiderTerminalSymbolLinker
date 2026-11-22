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
   public DraftOfAMessage Draft { get; } = new();
   public bool IsSending => _submission is not null;
   public string? Notice { get; private set; }
   public event Action? Changed;

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
         PutBackIfPending($"The message is back in the box. {refusal.Message}");
      }
      catch(Exception failure)
      {
         PutBackIfPending($"{Unconfirmed} {failure.Message}");
      }
      finally
      {
         if(Notice == Sending) Notice = null;
         _submission = null;
         _submittingSteer = null;
         Changed?.Invoke();
      }

      void PutBackIfPending(string notice)
      {
         if(steer is not null && !_pendingSteers.Remove(steer)) return;
         Draft.PutBackAhead(message.Parts);
         Notice = notice;
      }
   }

   /// <summary>Matches messages by ID in live updates and reconnect snapshots. Restore omitted steers
   /// in submission order ahead of newer typing, even if the HTTP receipt has yet to arrive.</summary>
   public void Observe(CodexChatReading reading)
   {
      if(!reading.SupportsCodexSteerReceipts) return;
      var returned = false;
      var observed = false;
      for(var index = _pendingSteers.Count - 1; index >= 0; --index)
      {
         var steer = _pendingSteers[index];
         var turn = reading.Turns.FirstOrDefault(it => it.Id == steer.Turn.Value);
         if(turn is null) continue;
         if(!turn.RecordedClientMessageIds.Contains(steer.ClientUserMessageId))
         {
            if(turn.IsUnderWay) continue;
            Draft.PutBackAhead(steer.Parts);
            returned = true;
         }
         _pendingSteers.RemoveAt(index);
         observed = true;
      }
      if(returned) Tell("The conversation does not show your steer. It is back in the box. Check it before sending again.");
      else if(observed && Notice == Unconfirmed && _pendingSteers.Count == 0) Tell(null);
   }

   public void Tell(string? notice)
   {
      Notice = notice;
      Changed?.Invoke();
   }

   public SavedCodexDraft? AsSaved(CodexSessionId? session)
   {
      var parts = _submittingSteer is null && _submission is {} message ? Draft.WithMessageAhead(message) : Draft.Parts;
      var awaitingReceipt = IsSending && (_submittingSteer is null || _pendingSteers.Contains(_submittingSteer));
      var notice = awaitingReceipt ? Unconfirmed : Notice == Sending ? null : Notice;
      return parts.Count == 0 && _pendingSteers.Count == 0 ? null :
         new SavedCodexDraft(session?.Value, parts, notice, _pendingSteers.Count == 0 ? null : _pendingSteers.ToArray());
   }

   public void Restore(SavedCodexDraft saved)
   {
      Draft.PutBackAhead(saved.Parts);
      if(saved.PendingSteers is {} steers) _pendingSteers.AddRange(steers);
      Tell(saved.Notice);
   }
}
