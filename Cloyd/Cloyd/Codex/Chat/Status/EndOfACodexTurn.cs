using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat;
using Cloyd.Chat.Status;

namespace Cloyd.Codex.Chat.Status;

/// <summary>The outcome of a recorded turn and its timing, when Codex supplied it. Saved sessions
/// use the same duration and local completion time as a turn that just ended on screen.</summary>
sealed record EndOfACodexTurn(string Id, string Status, TimeTaken? Took, WhenItEnded? Ended) : ShownInTheChat
{
   public static EndOfACodexTurn? From(CodexChatTurn turn)
   {
      var duration = turn.DurationMs is {} ms ? TimeSpan.FromMilliseconds(ms) : turn.CompletedAt - turn.StartedAt;
      if(turn.Status is not ("completed" or "failed" or "interrupted")) return null;
      if(turn.Status == "completed" && duration is null && turn.CompletedAt is null) return null;
      return new(turn.Id, turn.Status, duration is {} took ? new TimeTaken(took) : null,
                 turn.CompletedAt is {} end ? WhenItEnded.AtUtc(end.UtcDateTime) : null);
   }

   public override string TextShown => Status switch
   {
      "completed" when Took is null => $"Done {Ended}",
      "completed" => $"Worked for {Took}" + (Ended is null ? "" : $" · done {Ended}"),
      _ => (Status == "failed" ? "Turn failed" : "Turn interrupted") +
           (Took is null ? "" : $" after {Took}") + (Ended is null ? "" : $" · {Ended}")
   };
}
