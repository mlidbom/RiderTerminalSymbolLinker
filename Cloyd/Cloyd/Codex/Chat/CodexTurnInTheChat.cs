using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat;
using Cloyd.Codex.Chat.Status;

namespace Cloyd.Codex.Chat;

/// <summary>The rows a single Codex turn contributes to the conversation. Each replacement from the hub
/// rebuilds just this turn, keeping the already drawn rows of earlier turns.</summary>
sealed record CodexTurnInTheChat(string Id, CodexTurnProgress? Progress, IReadOnlyList<ShownInTheChat> Rows)
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
            "userMessage" when item.Details?.ImagePaths is not {Count: > 0} && ShownInTheChat.PeerMessage.FromCodex(id, item.Text) is {} peer => peer,
            "userMessage" => new ShownInTheChat.Prompt(id, PromptText(item)),
            "agentMessage" or "plan" when item.Text.Length > 0 => new CodexReply(id, item.Text),
            "contextCompaction" => new CodexChatNotice(id, "Compacted session"),
            _ => null
         };
         if(row is null) continue;
         EndRunOfCalls();
         rows.Add(row);
      }
      EndRunOfCalls();
      if(EndOfACodexTurn.From(turn) is {} end) rows.Add(end);

      return new CodexTurnInTheChat(turn.Id, turn.Status == "inProgress" ? CodexTurnProgress.From(turn) : null, rows);

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
         "mcpToolCall" or "dynamicToolCall" or "functionCallOutput" => "Tool call",
         "webSearch" => "Web search",
         "collabAgentToolCall" or "subAgentActivity" => "Agent activity",
         "imageView" => "Viewed image",
         "imageGeneration" => "Generated image",
         _ => null
      };
      if(verb is null) return null;
      var target = item.Kind switch
      {
         "commandExecution" when item.Details?.Command is {} command => command,
         "fileChange" when item.Details?.Changes is {Count: > 0} files => string.Join(", ", files.Select(it => it.Path)),
         "mcpToolCall" when item.Details?.Tool is {} tool => $"{item.Details.Server}/{tool}",
         _ => item.Text
      };
      var end = target.AsSpan().IndexOfAny('\r', '\n');
      var firstLine = end < 0 ? target : target[..end];
      var label = firstLine.Length > 100 ? firstLine[..100] + "…" : firstLine;
      return new CodexToolCall(id, label.Length == 0 ? verb : $"{verb} · {label}", CodexToolCallDetails.Of(item));
   }

   /// <summary>Show local image attachments by number, keeping any placeholder already present in the typed text.</summary>
   static string PromptText(CodexChatItem item)
   {
      if(item.Details?.ImagePaths is not {Count: > 0} images) return item.Text;
      var lines = item.Text.Length == 0 ? [] : item.Text.ReplaceLineEndings("\n").Split('\n').ToList();
      var withoutALine = new Stack<string>();
      for(var index = images.Count - 1; index >= 0; index--)
      {
         var marker = $"[Image #{index + 1}]";
         var at = lines.FindLastIndex(it => it == images[index]);
         if(at >= 0) lines.RemoveAt(at);
         if(lines.Any(it => it.Contains(marker, StringComparison.Ordinal))) continue;
         if(at >= 0) lines.Insert(at, marker);
         else withoutALine.Push(marker);
      }
      return string.Join("\n", lines.Concat(withoutALine));
   }
}
