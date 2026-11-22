using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat.Status;

namespace Cloyd.Codex.Chat.Status;

/// <summary>The activity, elapsed time and commands of the particular turn currently under way.
/// A snapshot taken halfway through a turn supplies the same identity as its subsequent updates.</summary>
sealed record CodexTurnProgress(CodexTurnId Id, DateTimeOffset? StartedAt, string Activity, int CommandCount, IReadOnlyList<CodexCallUnderWay> Calls)
{
   public static CodexTurnProgress From(CodexChatTurn turn)
   {
      var calls = turn.Items.Select(CodexCallUnderWay.From).OfType<CodexCallUnderWay>().ToArray();
      var activity = calls.Length > 0 ? "Running tools…" : turn.Items.LastOrDefault(it => it.Lifecycle == CodexChatItemLifecycle.InProgress)?.Kind switch
      {
         "agentMessage" => "Writing…",
         "reasoning" => "Thinking…",
         "plan" => "Planning…",
         "contextCompaction" => "Compacting session…",
         _ => "Working…"
      };
      return new CodexTurnProgress(new CodexTurnId(turn.Id), turn.StartedAt, activity,
                                   turn.Items.Count(it => it.Kind == "commandExecution"), calls);
   }

   public string CountsAt(DateTimeOffset now)
   {
      var parts = new List<string>();
      if(StartedAt is {} start) parts.Add(new TimeTaken(now - start).ToString());
      if(CommandCount > 0) parts.Add(CommandCount == 1 ? "1 command" : $"{CommandCount} commands");
      return parts.Count > 0 ? $" ({string.Join(" · ", parts)})" : "";
   }
}
