using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Cloyd.Chat.ToolCalls;
using Cloyd.Chat.Views;
using static Cloyd.Chat.Views.LinesOfTheChat;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>A message exchanged with another session. Its folded bubble identifies the peer and the
   /// protocol. Opening it shows the message and any recorded delivery details.</summary>
   public sealed partial record PeerMessage(string Id, string Peer, string Protocol, string Body) : ShownInTheChat
   {
      public bool IsOutgoing { get; private init; }
      public string? DeliveryDetails { get; private init; }
      public OutcomeOfAToolCall? DeliveryOutcome { get; private init; }
      string Status => DeliveryOutcome switch
      {
         OutcomeOfAToolCall.Failed => " · Failed",
         OutcomeOfAToolCall.Stopped => " · Stopped",
         _ => ""
      };
      string DirectionMark => IsOutgoing ? "↗ " : "";
      public override string TextShown => $"{DirectionMark}{Peer} · {Protocol}{Status}";

      public Control Draw(OpenedChatRows opened)
      {
         var accent = IsOutgoing ? ColoursOfTheChat.OutgoingPeerAccent : ColoursOfTheChat.IncomingPeerAccent;
         var name = Coloured(Peer, accent);
         name.FontWeight = FontWeight.Bold;
         var header = new List<Inline>();
         if(IsOutgoing)
         {
            var arrow = Coloured(DirectionMark, accent);
            arrow.FontFamily = new FontFamily("Segoe UI Symbol");
            header.Add(arrow);
         }
         header.Add(name);
         header.Add(Coloured($" · {Protocol}", ColoursOfTheChat.Grey));
         if(Status.Length > 0) header.Add(Coloured(Status, DeliveryOutcome == OutcomeOfAToolCall.Failed ? ColoursOfTheChat.Failed : ColoursOfTheChat.Grey));
         var direction = IsOutgoing ? "to" : "from";
         var row = Opening([.. header], Expanded, opened.IsOpen(Id), open => opened.SetOpen(Id, open), $"Message {direction} {Peer}, {Protocol}{Status}");
         var bubble = AgentBubble(row, IsOutgoing ? ColoursOfTheChat.OutgoingPeerBubble : ColoursOfTheChat.IncomingPeerBubble);
         bubble.BorderThickness = IsOutgoing ? new Thickness(0, 0, 4, 0) : new Thickness(4, 0, 0, 0);
         bubble.CornerRadius = IsOutgoing ? new CornerRadius(12, 4, 4, 12) : new CornerRadius(4, 12, 12, 4);
         return accent.AsTheBorderOf(bubble);
      }

      Control Expanded()
      {
         var body = ColoursOfTheChat.Text.AsTheForegroundOf(new SelectableTextBlock { Text = Body, TextWrapping = TextWrapping.Wrap });
         if(string.IsNullOrEmpty(DeliveryDetails)) return body;
         var receipt = ColoursOfTheChat.Grey.AsTheForegroundOf(new SelectableTextBlock { Text = DeliveryDetails, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
         return new StackPanel { Spacing = 6, Children = { body, receipt } };
      }
   }
}
