using Cloyd.Chat.Writing;
using Cloyd.Hub;
using Cloyd.Saving;

namespace Cloyd.Codex.Chat;

/// <summary>A single session's editor contents and submission. A receipt belongs here even when Terminal
/// switches to a different session while the hub is answering.</summary>
sealed class CodexSessionDraft
{
   const string Unconfirmed = "Delivery to Codex is unconfirmed. Check the conversation before sending again.";
   MessageFromTheChat? _submission;
   public DraftOfAMessage Draft { get; } = new();
   public bool IsSending => _submission is not null;
   public string? Notice { get; private set; }
   public event Action? Changed;

   public async Task Send(ICodexChatInTheHub hub, CodexSessionId session, CodexTurnId? steerInto)
   {
      var message = _submission = Draft.TakeTheMessage();
      Tell("Sending to Codex…");
      try
      {
         await hub.Send(session, message, steerInto);
         Notice = null;
      }
      catch(RefusedByTheHub refusal)
      {
         Draft.PutBackAhead(message.Parts);
         Notice = $"The message is back in the box. {refusal.Message}";
      }
      catch(Exception failure)
      {
         Draft.PutBackAhead(message.Parts);
         Notice = $"{Unconfirmed} {failure.Message}";
      }
      finally
      {
         _submission = null;
         Changed?.Invoke();
      }
   }

   public void Tell(string? notice)
   {
      Notice = notice;
      Changed?.Invoke();
   }

   public SavedCodexDraft? AsSaved(CodexSessionId? session)
   {
      var parts = _submission is {} message ? Draft.WithMessageAhead(message) : Draft.Parts;
      return parts.Count == 0 ? null : new SavedCodexDraft(session?.Value, parts, IsSending ? Unconfirmed : Notice);
   }

   public void Restore(SavedCodexDraft saved)
   {
      Draft.PutBackAhead(saved.Parts);
      Tell(saved.Notice);
   }
}
