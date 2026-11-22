using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat;

namespace Cloyd.Codex.Chat;

/// <summary>The rows a single Codex turn contributes to the conversation. Each replacement from the hub
/// rebuilds just this turn, keeping the already drawn rows of earlier turns.</summary>
sealed record CodexTurnInTheChat(string Id, bool IsUnderWay, IReadOnlyList<ShownInTheChat> Rows)
{
   public static CodexTurnInTheChat From(CodexChatTurn turn)
   {
      var rows = new List<ShownInTheChat>();
      var calls = new List<CodexToolCall>();
      foreach(var item in turn.Items)
      {
         var id = $"{turn.Id}/{item.Id}";
         if(Call(id, item) is {} call)
         {
            calls.Add(call);
            continue;
         }
         ShownInTheChat? row = item.Kind switch
         {
            "userMessage" => new ShownInTheChat.Prompt(id, item.Text),
            "agentMessage" or "plan" when item.Text.Length > 0 => new CodexReply(id, item.Text),
            "contextCompaction" => new CodexChatNotice(id, "Compacted session"),
            _ => null
         };
         if(row is null) continue;
         EndRunOfCalls();
         rows.Add(row);
      }
      EndRunOfCalls();
      if(turn.Status == "interrupted") rows.Add(new CodexChatNotice(turn.Id, "Turn interrupted"));
      if(turn.Status == "failed") rows.Add(new CodexChatNotice(turn.Id, "Turn failed"));

      return new CodexTurnInTheChat(turn.Id, turn.Status == "inProgress", rows);

      void EndRunOfCalls()
      {
         if(calls.Count == 0) return;
         rows.Add(new CodexToolCalls(calls.ToArray()));
         calls.Clear();
      }
   }

   static CodexToolCall? Call(string id, CodexChatItem item)
   {
      var verb = item.Kind switch
      {
         "commandExecution" => "Command",
         "fileChange" => "File change",
         "mcpToolCall" or "dynamicToolCall" => "Tool call",
         "webSearch" => "Web search",
         "collabAgentToolCall" or "subAgentActivity" => "Agent activity",
         "imageView" => "Viewed image",
         "imageGeneration" => "Generated image",
         _ => null
      };
      if(verb is null) return null;
      var end = item.Text.AsSpan().IndexOfAny('\r', '\n');
      var firstLine = end < 0 ? item.Text : item.Text[..end];
      var label = firstLine.Length > 100 ? firstLine[..100] + "…" : firstLine;
      return new CodexToolCall(id, label.Length == 0 ? verb : $"{verb} · {label}", null, item.Text, false);
   }
}
