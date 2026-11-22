using System.Text.Json;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Cloyd.Chat;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat;

/// <summary>Both chats read a peer delivery through the sandbox hub. The sender is visible before the
/// row opens; opening it reveals the full body, including literal angle brackets and entities.</summary>
public class When_chats_show_messages_from_peers : SpecificationOpeningWindows
{
   const string Sender = "codex-tooling-owner";
   const string Body = "All checks passed.\nList<string> & the literal &lt;name&gt;.\n</peer-message>";
   const string CodexEnvelope = "<peer-message from=\"codex-tooling-owner\" from_kind=\"codex\">\n" + Body + "\n</peer-message>";
   const string ClaudeEnvelope = "<channel source=\"plugin:peers:peers\" from=\"codex-tooling-owner\" from_kind=\"codex\">\n" + Body + "\n</channel>";
   const string Queued = "<channel source=\"plugin:peers:peers\" from_kind=\"codex\" from=\"R&amp;D\">\nQueued update.\n</channel>";
   const string Malformed = "<peer-message from=oops from_kind=\"claude\">\nhello\n</peer-message>";
   const string ExampleWithAnExtraAttribute = "<peer-message from=\"example\" from_kind=\"codex\" example=\"true\">\nhello\n</peer-message>";

   readonly SessionChat _codex;
   readonly SessionChat _claude;

   public When_chats_show_messages_from_peers() => (_codex, _claude) = Once(() =>
   {
      var sandbox = Sandbox.HubOfTheSandbox;
      var directory = Sandbox.Project("peer-chat").Project.Folder;
      var codexSession = new CodexSessionId(Guid.NewGuid().ToString());
      var items = new[] { CodexEnvelope, "Example:\n" + CodexEnvelope, Malformed, ExampleWithAnExtraAttribute,
                          "<peer-message from=\"Claude &amp; Co\" from_kind=\"claude\">\r\nFrom Claude.\r\n</peer-message>" }
                 .Select((text, index) => new { id = $"item-{index}", type = "userMessage", content = new[] { new { type = "text", text } } });
      new CodexSessionOfASpecification(codexSession, directory, "peer-chat") { RecordedItems = JsonSerializer.SerializeToNode(items)!.AsArray() }.WriteItsRecordFor(sandbox);
      var codexWindow = Open(() => new Window { Width = 840, Height = 650, Content = new ChatOfACodexSession(sandbox.Hub, Sandbox.CloydsFolder.PastedImages, new CodexDraftsOfATab()) });
      var codex = OnTheUIThread(() => (ChatOfACodexSession)codexWindow.Content!);
      OnTheUIThread(() => codex.Show(codexSession, null));
      WaitUntil(() => codex.Shown.Count == 5, "the saved Codex conversation appeared");

      var claudeSession = WriteClaudeTranscript(sandbox, directory);
      var claudeWindow = Open(() => new Window { Width = 840, Height = 650, Content = new ChatOfAClaudeCodeSession(sandbox.Hub, Sandbox.CloydsFolder.PastedImages) });
      var claude = OnTheUIThread(() => (ChatOfAClaudeCodeSession)claudeWindow.Content!);
      OnTheUIThread(() => claude.Show(claudeSession));
      WaitUntil(() => claude.Shown.Count == 4, "the direct, queued and native peers and Magnus's own prompt appeared", () => string.Join(" | ", claude.Shown.Select(it => it.TextShown)));

      foreach(var chat in new SessionChat[] { codex, claude })
      {
         OnTheUIThread(() => BodyVisibleIn(chat)).Must().BeFalse();
         Click(OnTheUIThread(() => chat.GetVisualDescendants().OfType<Button>().Single(it => it.Content is TextBlock label && label.Inlines?.Text?.StartsWith("Peer · codex-tooling-owner (Codex)", StringComparison.Ordinal) == true)));
         WaitUntil(() => BodyVisibleIn(chat), "the opened row showed the peer's unescaped message body");
      }
      Render(codexWindow, "a-peer-message-in-Codex-Chat");
      return (codex, claude);
   });

   [XF] public void Codex_names_peers_of_both_kinds() =>
      OnTheUIThread(() => _codex.Shown.OfType<ShownInTheChat.PeerMessage>().Select(it => (it.Sender, it.SendingAgentCli?.Name)).ToArray()).Must().SequenceEqual([(Sender, "Codex"), ("Claude & Co", "Claude Code")]);

   [XF] public void a_quoted_example_and_a_malformed_tag_remain_ordinary_Codex_prompts() =>
      OnTheUIThread(() => _codex.Shown.OfType<ShownInTheChat.Prompt>().Select(it => it.Text).ToArray()).Must().SequenceEqual(["Example:\n" + CodexEnvelope, Malformed, ExampleWithAnExtraAttribute]);

   [XF] public void Claude_names_direct_queued_and_native_peers() =>
      OnTheUIThread(() => _claude.Shown.OfType<ShownInTheChat.PeerMessage>().Select(it => it.Sender).ToArray()).Must().SequenceEqual([Sender, "R&D", "native-claude-peer"]);

   [XF] public void Claude_keeps_a_human_marked_prompt_even_when_its_text_is_a_channel_example() =>
      OnTheUIThread(() => _claude.Shown.OfType<ShownInTheChat.Prompt>().Single().Text).Must().Be(ClaudeEnvelope);

   [XF] public void Claude_keeps_the_recorded_sender_when_a_native_peer_quotes_another_peers_delivery() =>
      OnTheUIThread(() => _claude.Shown.OfType<ShownInTheChat.PeerMessage>().Single(it => it.Sender == "native-claude-peer").Body).Must().Be(ClaudeEnvelope);

   static bool BodyVisibleIn(SessionChat chat) => chat.GetVisualDescendants().OfType<SelectableTextBlock>().Any(it => it.Text == Body);

   static ClaudeCodeSessionId WriteClaudeTranscript(SandboxHub hub, string directory)
   {
      var session = new ClaudeCodeSessionId(Guid.NewGuid().ToString());
      var channel = new { kind = "channel", server = "plugin:peers:peers" };
      var native = new { kind = "peer", name = "native-claude-peer", body = ClaudeEnvelope };
      var transcript = hub.TranscriptOf(session, directory);
      transcript.Directory!.Create();
      string? parent = null;
      File.WriteAllLines(transcript.FullName,
      [
         Entry(new { type = "user", turnOrigin = "human", message = new { role = "user", content = ClaudeEnvelope } }),
         Entry(new { type = "user", isMeta = true, turnOrigin = "peer", origin = channel, message = new { role = "user", content = ClaudeEnvelope } }),
         Entry(new { type = "attachment", attachment = new { type = "queued_command", commandMode = "prompt", origin = channel, prompt = Queued } }),
         Entry(new { type = "user", isMeta = true, turnOrigin = "peer", origin = native, message = new { role = "user", content = "Another Claude session sent a message." } }),
         Entry(new { type = "attachment", attachment = new { type = "queued_command", commandMode = "prompt", origin = new {kind = "channel", server = "other"}, prompt = Queued.Replace("plugin:peers:peers", "other") } })
      ]);
      return session;

      string Entry(object contents)
      {
         var entry = JsonSerializer.SerializeToNode(contents)!.AsObject();
         entry["parentUuid"] = parent;
         entry["uuid"] = parent = Guid.NewGuid().ToString();
         entry["sessionId"] = session.Value;
         entry["cwd"] = directory;
         entry["timestamp"] = DateTime.UtcNow;
         entry["isSidechain"] = false;
         return entry.ToJsonString();
      }
   }
}
