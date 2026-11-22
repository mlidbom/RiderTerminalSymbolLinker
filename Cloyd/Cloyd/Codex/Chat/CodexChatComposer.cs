using Cloyd.Chat.Writing;
using Cloyd.Hub;
using Cloyd.Pasting;

namespace Cloyd.Codex.Chat;

/// <summary>The editor of a Codex chat. Send gives input to the session and turn last observed in this tab.
/// A refused or unconfirmed submission goes back into the editor for Magnus to decide what to do with it.</summary>
sealed class CodexChatComposer : ChatComposer
{
   readonly ICodexChatInTheHub _hub;
   readonly CodexDraftsOfATab _drafts;
   CodexSessionDraft _input;
   CodexSessionId? _session;
   CodexTurnId? _turn;
   bool _ready;
   bool _stopping;

   public CodexChatComposer(ICodexChatInTheHub hub, PastedImages pastedImages, CodexDraftsOfATab drafts) : base(pastedImages, "Reply to Codex.", "Stop Codex")
   {
      _hub = hub;
      _drafts = drafts;
      _input = drafts.For(null);
      _input.Changed += Refresh;
      Edit(_input.Draft);
      Refresh();
   }

   /// <summary>Editing continues during disconnection. Only an observed running session can take new commands.</summary>
   public void Target(CodexSessionId? session, bool ready, CodexTurnId? turn)
   {
      _session = session;
      _ready = ready && session is not null;
      _turn = turn;
      var input = _drafts.For(session);
      if(_input != input)
      {
         _input.Changed -= Refresh;
         _input = input;
         _input.Changed += Refresh;
         Edit(input.Draft);
      }
      Refresh();
   }

   void Refresh()
   {
      CanSend = _ready && !_input.IsSending;
      ShowsTheStopButton = _ready && _turn is not null;
      Tell(_input.Notice);
   }

   public void Observe(CodexSessionId session, CodexChatReading reading) => _drafts.For(session).Observe(reading);

   protected override void Send()
   {
      if(Draft.IsEmpty || _input.IsSending) return;
      if(!_ready || _session is not {} session)
      {
         _input.Tell("Waiting for the current Codex session. Your message is still in the box.");
         return;
      }
      if(CodexTerminalCommand.WasTypedIn(Draft))
      {
         _input.Tell("Use Terminal (Ctrl+H) for Codex slash commands.");
         return;
      }
      _ = _input.Send(_hub, session, _turn);
   }

   protected override async Task Stop()
   {
      if(!_ready || _session is not {} session || _turn is not {} turn || _stopping) return;
      _stopping = true;
      var input = _input;
      try
      {
         await _hub.Interrupt(session, turn);
      }
      catch(RefusedByTheHub refusal) { input.Tell($"Codex refused to stop the turn. {refusal.Message}"); }
      catch(Exception failure) { input.Tell($"Stopping the Codex turn is unconfirmed. {failure.Message}"); }
      finally { _stopping = false; }
   }
}
