using System.Xml;
using System.Xml.Linq;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   public sealed partial record PeerMessage
   {
      /// <summary>Peers delivers to Codex as a text prompt enclosed in its own tag. Codex history retains that text.</summary>
      public static PeerMessage? FromCodex(string id, string text) => FromEnvelope(id, text, "peer-message");

      /// <summary>Claude Code records native peers by name. The Peers channel can arrive as another session
      /// or among Claude Code's own notices, with the sender in the channel header.</summary>
      public static PeerMessage? FromClaudeCode(MessageInTheAnswer.PromptFromElsewhere prompt)
      {
         if(prompt is { From: "anotherSession", SendingSessionName: {Length: > 0} name }) return new(prompt.Id, name, "Claude Code", prompt.Text);
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
         var kind = (string?)opening.Attribute("from_kind");
         if(string.IsNullOrWhiteSpace(sender) || kind is not ("codex" or "claude")) return null;
         var body = text.AsSpan(firstBreak + 1, lastBreak - firstBreak - 1);
         if(text[firstBreak - 1] == '\r' && body.EndsWith("\r")) body = body[..^1];
         return new PeerMessage(id, sender, "Peers", body.ToString());
      }
   }
}
