using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat;
using Cloyd.Chat.Status;

namespace Cloyd.Codex.Chat.Status;

/// <summary>A tool Codex is currently running. Its input remains visible above the composer
/// even when Magnus scrolls back through the conversation.</summary>
sealed record CodexCallUnderWay(string Description, string? Input, DateTimeOffset? StartedAt)
{
   public static CodexCallUnderWay? From(CodexChatItem item)
   {
      if((item.Lifecycle ?? item.Details?.Status) != CodexChatItemLifecycle.InProgress) return null;
      var details = item.Details;
      if(ShownInTheChat.PeerMessage.IsCodexSend(item)) return new("Sending a peer message", null, item.StartedAt);
      return item.Kind switch
      {
         "commandExecution" => new("Running command", details?.Command, item.StartedAt),
         "fileChange" => new("Changing files", details?.Changes is {Count: > 0} changes ? string.Join("\n", changes.Select(it => it.MovePath is null ? it.Path : $"{it.Path} → {it.MovePath}")) : null, item.StartedAt),
         "mcpToolCall" or "dynamicToolCall" => new(details?.Tool is {} tool ? $"Calling {string.Join("/", new[] { details.Server, tool }.OfType<string>())}" : "Calling tool", CodexToolCallDetails.Of(item).Input, item.StartedAt),
         "webSearch" => new("Searching the web", item.Text, item.StartedAt),
         "imageGeneration" => new("Generating image", null, item.StartedAt),
         "imageView" => new("Viewing image", item.Text, item.StartedAt),
         "collabAgentToolCall" or "subAgentActivity" => new("Working with agents", item.Text, item.StartedAt),
         _ => null
      };
   }

   public string HeadingAt(DateTimeOffset now, int otherCalls) =>
      Description + (StartedAt is {} start ? $" · {new TimeTaken(now - start)}" : "") + (otherCalls > 0 ? $" · {otherCalls} more running" : "");

   public string? PreviewOfInput
   {
      get
      {
         if(string.IsNullOrWhiteSpace(Input)) return null;
         var lines = Input.ReplaceLineEndings("\n").Split('\n');
         var first = string.Join("\n", lines.Take(3));
         return lines.Length > 3 ? first + "…" : first;
      }
   }
}
