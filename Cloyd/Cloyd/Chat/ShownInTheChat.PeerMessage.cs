using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;
using Cloyd.Chat.Views;
using Cloyd.Sessions;
using static Cloyd.Chat.Views.LinesOfTheChat;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>A message sent by another agent session. Its collapsed row names the sender and previews
   /// the message. Opening the row shows the original, selectable <see cref="Body"/>.</summary>
   public sealed record PeerMessage(string Id, string Sender, AgentCli? SendingAgentCli, string Body) : ShownInTheChat
   {
      string Heading => SendingAgentCli is null ? $"Peer · {Sender}" : $"Peer · {Sender} ({SendingAgentCli.Name})";

      string PreviewAfterHeading
      {
         get
         {
            var text = Body.AsSpan().TrimStart();
            var end = text.IndexOfAny('\r', '\n');
            var first = end < 0 ? text : text[..end];
            const int limit = 96;
            if(first.IsEmpty) return "";
            if(first.Length <= limit) return $" — {first}";
            var cut = char.IsHighSurrogate(first[limit - 1]) ? limit - 1 : limit;
            return $" — {first[..cut].TrimEnd()}…";
         }
      }

      public override string TextShown => Heading + PreviewAfterHeading;

      public Control Draw(OpenedChatRows opened) =>
         Opening([Coloured(Heading, ColoursOfTheChat.Text), Coloured(PreviewAfterHeading, ColoursOfTheChat.Grey)],
                 () => ColoursOfTheChat.Text.AsTheForegroundOf(new SelectableTextBlock { Text = Body, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 2, 0, 4) }),
                 opened.IsOpen(Id), open => opened.SetOpen(Id, open));

      /// <summary>Peers delivers to Codex as a text prompt enclosed in its own tag. Codex history retains that text.</summary>
      public static PeerMessage? FromCodex(string id, string text) => FromEnvelope(id, text, "peer-message");

      /// <summary>Claude Code records native peers by name. The Peers channel can arrive as another session
      /// or among Claude Code's own notices, with the sender in the channel header.</summary>
      public static PeerMessage? FromClaudeCode(MessageInTheAnswer.PromptFromElsewhere prompt)
      {
         if(prompt is { From: "anotherSession", SendingSessionName: {Length: > 0} name }) return new(prompt.Id, name, null, prompt.Text);
         if(prompt.From is not ("anotherSession" or "claudeCode")) return null;
         return FromEnvelope(prompt.Id, prompt.Text, "channel");
      }

      /// <summary>Only the opening tag is markup. The body may contain literal XML, shell redirection,
      /// ampersands or another message quoted by the sender.</summary>
      static PeerMessage? FromEnvelope(string id, string text, string tag)
      {
         if(!text.StartsWith($"<{tag} ", StringComparison.Ordinal) || !text.EndsWith($"\n</{tag}>", StringComparison.Ordinal)) return null;
         var firstBreak = text.IndexOf('\n');
         var lastBreak = text.LastIndexOf('\n');
         if(firstBreak == lastBreak) return null;
         var header = text.AsSpan(0, firstBreak).TrimEnd('\r');
         if(!header.EndsWith(">")) return null;
         XElement opening;
         try { opening = XElement.Parse(header[..^1].ToString() + "/>"); }
         catch(XmlException) { return null; } // A prompt can quote an incomplete or malformed header.
         if(opening.Name != tag || (tag == "channel" && (string?)opening.Attribute("source") != "plugin:peers:peers")) return null;
         if(tag == "peer-message" && opening.Attributes().Count() != 2) return null;
         var sender = (string?)opening.Attribute("from");
         var cli = (string?)opening.Attribute("from_kind") switch { "codex" => AgentCli.Codex, "claude" => AgentCli.ClaudeCode, _ => null };
         if(string.IsNullOrWhiteSpace(sender) || cli is null) return null;
         var body = text.AsSpan(firstBreak + 1, lastBreak - firstBreak - 1);
         if(text[firstBreak - 1] == '\r' && body.EndsWith("\r")) body = body[..^1];
         return new PeerMessage(id, sender, cli, body.ToString());
      }
   }
}
