using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat;
using Cloyd.Chat.ToolCalls;
using Cloyd.Codex.Chat;
using Cloyd.Codex.Chat.Status;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

public class When_Codex_records_messages_between_sessions
{
   const string Thread = "01a202a9-62df-7383-afdb-afb4cc23f001";
   static readonly CodexChatItem NativeSend = new("send", "mcpToolCall", "", null) { Details = new() { Server = "codex_tui", Tool = "send_message_to_thread",
      Arguments = JsonSerializer.SerializeToElement(new { threadId = Thread, prompt = "Is the build ready?" }) } };
   static readonly CodexChatItem Delivery = new("receive", "functionCallOutput", "", null) { Details = new() { Server = "codex_tui", Tool = "send_message_to_thread",
      Result = JsonSerializer.SerializeToElement($"<codex_delegation>\n  <source_thread_id>{Thread}</source_thread_id>\n  <input>Text &amp; details</input>\n</codex_delegation>") } };

   [XF] public void native_Codex_sends_and_deliveries_each_have_one_row_between_the_ordinary_tools()
   {
      var ordinaryTool = NativeSend with { Details = NativeSend.Details! with { Server = "another-server" } };
      var rows = CodexTurnInTheChat.From(new CodexChatTurn("turn", "inProgress", [ordinaryTool, NativeSend, Delivery, ordinaryTool with { Id = "next" }])).Rows;
      (rows is [CodexToolCalls, ShownInTheChat.PeerMessage { Protocol: "Codex", Peer: Thread, IsOutgoing: true, Body: "Is the build ready?" },
                                ShownInTheChat.PeerMessage { Protocol: "Codex", Peer: Thread, IsOutgoing: false, Body: "Text & details" }, CodexToolCalls]).Must().BeTrue();
   }

   [XF] public void the_composer_does_not_preview_the_message_being_sent() =>
      CodexCallUnderWay.From(NativeSend with { Lifecycle = "inProgress" }).Must().Be(new CodexCallUnderWay("Sending a peer message", null, null));

   [XF] public void native_desktop_deliveries_use_the_same_Codex_protocol() =>
      ShownInTheChat.PeerMessage.FromCodexTool("desktop", Delivery with { Details = Delivery.Details! with { Server = "codex_app" } })!.Body.Must().Be("Text & details");

   [XF] public void a_late_peer_name_keeps_the_reading_range_in_a_long_conversation()
   {
      var drawing = new DrawnEndOfTheConversation();
      var peer = ShownInTheChat.PeerMessage.FromCodexTool("receive", Delivery)!;
      ShownInTheChat[] rows = [.. Enumerable.Range(0, 300).Select(index => new ShownInTheChat.Prompt(index.ToString(), "Prompt"))];
      rows[130] = peer;
      drawing.Show(rows);
      drawing.DrawMoreAbove();
      var first = drawing.FirstDrawn;
      var relabelled = rows.ToArray();
      relabelled[130] = peer with { Peer = "cloyd-gpt" };
      drawing.RefreshLabels(relabelled);
      drawing.FirstDrawn.Must().Be(first);
      drawing.Drawn[130 - first].Must().Be(relabelled[130]);
   }

   [XF] public void failed_delivery_is_visible_and_its_details_are_preserved_for_expansion()
   {
      var refused = NativeSend with { Details = NativeSend.Details! with { Status = "failed", Error = JsonSerializer.SerializeToElement("Session is closed.") } };
      var message = ShownInTheChat.PeerMessage.FromCodexTool("refused", refused)!;
      message.DeliveryOutcome.Must().Be(OutcomeOfAToolCall.Failed);
      message.TextShown.Must().Contain("Failed");
      message.DeliveryDetails.Must().Be("Session is closed.");
   }

   [XF] public void a_tool_result_quoting_a_delegation_is_not_an_incoming_message() =>
      ShownInTheChat.PeerMessage.FromCodexTool("example", Delivery with { Details = Delivery.Details! with { Server = "another-server" } }).Must().BeNull();
}
