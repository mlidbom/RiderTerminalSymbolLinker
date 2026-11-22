using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat;

namespace Cloyd.Codex.Chat;

/// <summary>One subscription's ordered Codex turns. The first snapshot replaces the entire conversation.
/// Thereafter the hub sends a whole turn whenever one of its items changes.</summary>
sealed class CodexConversation(CodexChatSnapshot snapshot)
{
   readonly List<CodexTurnInTheChat> _turns = [.. snapshot.Turns.Select(CodexTurnInTheChat.From)];
   readonly bool _supportsSteerReceipts = snapshot.SupportsCodexSteerReceipts;

   public void Update(CodexChatTurn turn)
   {
      var replacement = CodexTurnInTheChat.From(turn);
      var at = _turns.FindIndex(it => it.Id == turn.Id);
      if(at < 0) _turns.Add(replacement);
      else _turns[at] = replacement;
   }

   public CodexChatReading AsShown() =>
      new([.. _turns.SelectMany(it => it.Rows)], _turns.LastOrDefault(it => it.IsUnderWay) is {} active ? new CodexTurnId(active.Id) : null)
      { Turns = _turns.ToArray(), SupportsCodexSteerReceipts = _supportsSteerReceipts };
}
