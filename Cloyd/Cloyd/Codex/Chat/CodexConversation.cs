using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;

namespace Cloyd.Codex.Chat;

/// <summary>One subscription's ordered Codex turns. The first snapshot replaces the entire conversation.
/// Thereafter the hub sends a whole turn whenever one of its items changes.</summary>
sealed class CodexConversation
{
   readonly string _sessionId;
   readonly bool _supportsSteerReceipts;
   readonly List<CodexTurnInTheChat> _turns;
   CodexChatFooter? _footer;

   public CodexConversation(CodexChatSnapshot snapshot)
   {
      _sessionId = snapshot.SessionId;
      _supportsSteerReceipts = snapshot.SupportsCodexSteerReceipts;
      _turns = [.. snapshot.Turns.Select(CodexTurnInTheChat.From)];
      if(snapshot.Footer is {} footer) Update(footer);
   }

   public void Update(CodexChatFooter footer)
   {
      if(footer.SessionId != _sessionId) throw new InvalidDataException("The hub sent a footer for a different Codex session.");
      _footer = footer;
   }

   public void Update(CodexChatTurn turn)
   {
      var replacement = CodexTurnInTheChat.From(turn);
      var at = _turns.FindIndex(it => it.Id == turn.Id);
      if(at < 0) _turns.Add(replacement);
      else _turns[at] = replacement;
   }

   public CodexChatReading AsShown() =>
      new([.. _turns.SelectMany(it => it.Rows)], _turns.LastOrDefault()?.Progress, _footer)
      { Turns = _turns.ToArray(), SupportsCodexSteerReceipts = _supportsSteerReceipts };
}
