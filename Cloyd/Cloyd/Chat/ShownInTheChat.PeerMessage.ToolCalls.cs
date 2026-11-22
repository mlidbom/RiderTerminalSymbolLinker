using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat.ToolCalls;
using Cloyd.Codex.Chat;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   public sealed partial record PeerMessage
   {
      /// <summary>Claude Code records a send as a tool call. Its result can arrive later in the transcript.</summary>
      public static PeerMessage? SentByClaude(ToolCallInTheChat tool)
      {
         var call = tool.Call;
         var input = call.Input;
         var message = call.Name switch
         {
            "SendMessage" when Field(input, "type") is null or "message" =>
               Sent(call.Id, "Claude Code", Field(input, "to") ?? Field(input, "recipient"), Field(input, "message") ?? Field(input, "content")),
            "mcp__plugin_peers_peers__send_to_peer" or "mcp__peers__send_to_peer" =>
               Sent(call.Id, "Peers", Field(input, "to"), Field(input, "message")),
            _ => null
         };
         return message is null ? null : message with { DeliveryDetails = tool.Result?.Text ?? "Sending…", DeliveryOutcome = tool.Outcome };
      }

      /// <summary>Identifies outgoing sends before arguments arrive, so live progress can keep the body folded too.</summary>
      public static bool IsCodexSend(CodexChatItem item) => item.Kind == "mcpToolCall" && item.Details is
         { Server: "peers", Tool: "send_to_peer" } or { Server: "codex_tui" or "codex_app", Tool: "send_message_to_thread" };

      /// <summary>Recognizes the actual messaging tools by their server and tool names. Native Codex also
      /// records incoming thread messages as unsolicited tool outputs.</summary>
      public static PeerMessage? FromCodexTool(string id, CodexChatItem item)
      {
         if(item.Details is not {} details) return null;
         if(item.Kind == "functionCallOutput" && details is { Server: "codex_tui" or "codex_app", Tool: "send_message_to_thread", Result: { ValueKind: JsonValueKind.String } output })
            return NativeCodexDelivery(id, output.GetString()!);
         if(!IsCodexSend(item) || details.Arguments is not { ValueKind: JsonValueKind.Object } input) return null;
         var message = details.Server == "peers" ? Sent(id, "Peers", Field(input, "to"), Field(input, "message"))
                                                 : Sent(id, "Codex", Field(input, "threadId"), Field(input, "prompt"));
         if(message is null) return null;
         var outcome = CodexToolCallDetails.Of(item);
         var reportedError = details.Result is { ValueKind: JsonValueKind.Object } result && result.TryGetProperty("isError", out var error) && error.ValueKind == JsonValueKind.True;
         return message with
         {
            DeliveryDetails = outcome.Output ?? outcome.Outcome,
            DeliveryOutcome = details.Status == "declined" ? OutcomeOfAToolCall.Stopped : outcome.Failed || reportedError ? OutcomeOfAToolCall.Failed : null
         };
      }

      static PeerMessage? Sent(string id, string protocol, string? peer, string? body)
      {
         if(string.IsNullOrWhiteSpace(peer) || string.IsNullOrEmpty(body)) return null;
         if(protocol == "Peers" && (peer.StartsWith("codex:", StringComparison.Ordinal) || peer.StartsWith("claude:", StringComparison.Ordinal)))
            peer = peer[(peer.IndexOf(':') + 1)..];
         return new PeerMessage(id, peer, protocol, body) { IsOutgoing = true };
      }

      static string? Field(JsonElement data, string name) =>
         data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

      static PeerMessage? NativeCodexDelivery(string id, string text)
      {
         if(!text.StartsWith("<codex_delegation>", StringComparison.Ordinal)) return null;
         XElement envelope;
         try { envelope = XElement.Parse(text, LoadOptions.PreserveWhitespace); }
         catch(XmlException) { return null; } // An unrelated or malformed tool result remains available as a tool call.
         if(envelope.Elements().ToArray() is not [var source, var input] || source.Name != "source_thread_id"
            || !Guid.TryParse(source.Value, out _) || input.Name != "input" || input.HasElements) return null;
         return new PeerMessage(id, source.Value, "Codex", input.Value);
      }
   }
}
