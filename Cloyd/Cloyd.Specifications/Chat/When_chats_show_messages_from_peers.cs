using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Cloyd.Chat;
using Cloyd.Chat.Views;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;
using LiveMarkdown.Avalonia;

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
      var recorded = JsonSerializer.SerializeToNode(items)!.AsArray();
      recorded.Add(JsonNode.Parse("""{"type":"mcpToolCall","id":"send","server":"peers","tool":"send_to_peer","arguments":{"to":"codex:cloyd-gpt","message":"Please check the build."},"status":"completed","result":{"content":[{"type":"text","text":"Queued for cloyd-gpt."}]}}"""));
      recorded.Add(JsonNode.Parse("""{"type":"agentMessage","id":"reply","text":"The build passed."}"""));
      new CodexSessionOfASpecification(codexSession, directory, "peer-chat") { RecordedItems = recorded }.WriteItsRecordFor(sandbox);
      var codexWindow = Open(() => new Window { Width = 840, Height = 650, Content = new ChatOfACodexSession(sandbox.Hub, Sandbox.CloydsFolder.PastedImages, new CodexDraftsOfATab()) });
      var codex = OnTheUIThread(() => (ChatOfACodexSession)codexWindow.Content!);
      OnTheUIThread(() => codex.Show(codexSession, null));
      WaitUntil(() => codex.Shown.Count == 7, "the saved Codex conversation appeared");

      var claudeSession = WriteClaudeTranscript(sandbox, directory);
      var claudeWindow = Open(() => new Window { Width = 840, Height = 650, Content = new ChatOfAClaudeCodeSession(sandbox.Hub, Sandbox.CloydsFolder.PastedImages) });
      var claude = OnTheUIThread(() => (ChatOfAClaudeCodeSession)claudeWindow.Content!);
      OnTheUIThread(() => claude.Show(claudeSession));
      WaitUntil(() => claude.Shown.Count == 7, "the incoming messages, sends, reply and Magnus's own prompt appeared", () => string.Join(" | ", claude.Shown.Select(it => it.TextShown)));

      foreach(var chat in new SessionChat[] { codex, claude })
      {
         OnTheUIThread(() => BodyVisibleIn(chat)).Must().BeFalse();
         Click(OnTheUIThread(() => chat.GetVisualDescendants().OfType<Button>().Single(it => AutomationProperties.GetName(it) == $"Message from {Sender}, Peers")));
         WaitUntil(() => BodyVisibleIn(chat), "the opened row showed the peer's unescaped message body");
      }
      Render(codexWindow, "a-peer-message-in-Codex-Chat");
      return (codex, claude);
   });

   [XF] public void Codex_labels_Peers_deliveries_by_sender_and_protocol_and_shows_each_send_once() =>
      OnTheUIThread(() => _codex.Shown.OfType<ShownInTheChat.PeerMessage>().Select(it => it.TextShown).ToArray())
        .Must().SequenceEqual([Sender + " · Peers", "Claude & Co · Peers", "↗ cloyd-gpt · Peers"]);

   [XF] public void a_quoted_example_and_a_malformed_tag_remain_ordinary_Codex_prompts() =>
      OnTheUIThread(() => _codex.Shown.OfType<ShownInTheChat.Prompt>().Select(it => it.Text).ToArray()).Must().SequenceEqual(["Example:\n" + CodexEnvelope, Malformed, ExampleWithAnExtraAttribute]);

   [XF] public void Claude_names_direct_queued_and_native_peers() =>
      OnTheUIThread(() => _claude.Shown.OfType<ShownInTheChat.PeerMessage>().Select(it => it.TextShown).ToArray())
        .Must().SequenceEqual([Sender + " · Peers", "R&D · Peers", "native-claude-peer · Claude Code", "↗ cloyd-gpt · Peers", "↗ native-claude-peer · Claude Code · Stopped"]);

   [XF] public void Claude_keeps_a_human_marked_prompt_even_when_its_text_is_a_channel_example() =>
      OnTheUIThread(() => _claude.Shown.OfType<ShownInTheChat.Prompt>().Single().Text).Must().Be(ClaudeEnvelope);

   [XF] public void Claude_keeps_the_recorded_sender_when_a_native_peer_quotes_another_peers_delivery() =>
      OnTheUIThread(() => _claude.Shown.OfType<ShownInTheChat.PeerMessage>().Single(it => it.Peer == "native-claude-peer" && !it.IsOutgoing).Body).Must().Be(ClaudeEnvelope);

   [XF] public void folded_peer_bubbles_are_borderless_and_one_message_line_high_in_the_light_theme() => CheckBubbles(ThemeVariant.Light);

   [XF] public void folded_peer_bubbles_are_borderless_and_one_message_line_high_in_the_dark_theme() => CheckBubbles(ThemeVariant.Dark);

   [XF] public void a_long_peer_name_stays_on_one_line_until_opened_in_a_narrow_window()
   {
      var peer = new ShownInTheChat.PeerMessage("narrow", "A session name long enough to wrap several times in this window", "Peers", "Body on line one.\nAnd line two.");
      var window = Open(() => new Window { Width = 230, Height = 260, Content = new StackPanel
      {
         Children = { peer.Draw(new OpenedChatRows()), LinesOfTheChat.Bubble(new Run("One line.")) }
      } });
      var rows = OnTheUIThread(() => (StackPanel)window.Content!);
      WaitUntil(() => rows.Children.All(it => it.Bounds.Height > 0), "both bubbles were laid out");
      var (foldedHeight, singleLineHeight) = OnTheUIThread(() => (rows.Children[0].Bounds.Height, rows.Children[1].Bounds.Height));
      foldedHeight.Must().Satisfy(it => it <= singleLineHeight);
      var header = OnTheUIThread(() => rows.GetVisualDescendants().OfType<Button>().Single());
      Click(header);
      WaitUntil(() => rows.Children[0].Bounds.Height > foldedHeight && BodyIsVisible(), "the full message opened");
      Click(header);
      WaitUntil(() => rows.Children[0].Bounds.Height <= singleLineHeight && !BodyIsVisible(), "the bubble returned to one line");

      bool BodyIsVisible() => rows.GetVisualDescendants().OfType<SelectableTextBlock>().Any(it => it.Text == peer.Body);
   }

   [XF] public void a_sent_message_opens_to_its_body_and_receipt_and_folds_away_again()
   {
      foreach(var chat in new[] { _claude, _codex })
      {
         var button = OnTheUIThread(() => chat.GetVisualDescendants().OfType<Button>().Single(it => AutomationProperties.GetName(it) == "Message to cloyd-gpt, Peers"));
         Click(button);
         WaitUntil(() => HasText("Please check the build.") && HasText("Queued for cloyd-gpt."), "the sent message and its receipt appeared");
         Click(button);
         WaitUntil(() => !HasText("Please check the build.") && !HasText("Queued for cloyd-gpt."), "the body and receipt folded away");

         bool HasText(string value) => chat.GetVisualDescendants().OfType<SelectableTextBlock>().Any(it => it.Text == value);
      }
   }

   void CheckBubbles(ThemeVariant theme)
   {
      foreach(var chat in new[] { _claude, _codex })
      {
         var window = OnTheUIThread(() => (Window)TopLevel.GetTopLevel(chat)!);
         OnTheUIThread(() => window.RequestedThemeVariant = theme);
         WaitUntil(() => chat.ActualThemeVariant == theme, "the chat took the requested theme");
         OnTheUIThread(() =>
         {
            var borders = chat.GetVisualDescendants().OfType<Border>().ToArray();
            var foldedSender = chat == _claude ? "R&D" : "Claude & Co";
            var incoming = borders.Single(it => it.Child is StackPanel && it.GetVisualDescendants().OfType<Button>().Any(button => AutomationProperties.GetName(button) == $"Message from {foldedSender}, Peers"));
            var outgoing = borders.Single(it => it.Child is StackPanel && it.GetVisualDescendants().OfType<Button>().Any(button => AutomationProperties.GetName(button) == "Message to cloyd-gpt, Peers"));
            incoming.BorderThickness.Must().Be(new Thickness(0));
            outgoing.BorderThickness.Must().Be(new Thickness(0));
            ((ISolidColorBrush)incoming.Background!).Color.Must().Be(ColoursOfTheChat.IncomingPeerBubble.In(theme));
            ((ISolidColorBrush)outgoing.Background!).Color.Must().Be(ColoursOfTheChat.OutgoingPeerBubble.In(theme));
            outgoing.GetVisualDescendants().OfType<SelectableTextBlock>().Any(it => it.Text == "Please check the build.").Must().BeFalse();
            var reply = borders.Single(it => it.Child is MarkdownRenderer);
            reply.Bounds.Height.Must().Satisfy(it => it > 0);
            incoming.Bounds.Height.Must().Satisfy(it => it > 0 && it <= reply.Bounds.Height);
            outgoing.Bounds.Height.Must().Satisfy(it => it > 0 && it <= reply.Bounds.Height);
            reply.HorizontalAlignment.Must().Be(HorizontalAlignment.Left);
            ((ISolidColorBrush)reply.Background!).Color.Must().Be(ColoursOfTheChat.ReplyBubble.In(theme));
            borders.Any(it => it.HorizontalAlignment == HorizontalAlignment.Right
                              && it.Background is ISolidColorBrush brush && brush.Color == ColoursOfTheChat.BubbleOfAPrompt.In(theme)).Must().BeTrue();
         });
         Render(window, $"peer-message-directions-{chat.GetType().Name}-{theme}");
      }
   }

   static bool BodyVisibleIn(SessionChat chat) => chat.GetVisualDescendants().OfType<SelectableTextBlock>().Any(it => it.Text == Body);

   [XF] public void a_Claude_send_denied_in_the_terminal_keeps_its_stopped_outcome() => OnTheUIThread(() =>
   {
      var stopped = _claude.Shown.OfType<ShownInTheChat.PeerMessage>().Single(it => it.IsOutgoing && it.Protocol == "Claude Code");
      stopped.TextShown.Must().Contain("Stopped");
      stopped.DeliveryDetails.Must().Contain("The user doesn't want to proceed with this tool use.");
   });

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
         Entry(new { type = "attachment", attachment = new { type = "queued_command", commandMode = "prompt", origin = new {kind = "channel", server = "other"}, prompt = Queued.Replace("plugin:peers:peers", "other") } }),
         Entry(new { type = "assistant", message = new { role = "assistant", id = "reply", content = new object[]
         {
            new { type = "tool_use", id = "send1", name = "mcp__plugin_peers_peers__send_to_peer", input = new { to = "codex:cloyd-gpt", message = "Please check the build." } },
            new { type = "tool_use", id = "send2", name = "SendMessage", input = new { to = "native-claude-peer", message = "Please check the build." } },
            new { type = "text", text = "The build passed." }
         } } }),
         Entry(new { type = "user", message = new { role = "user", content = new[] { new { type = "tool_result", tool_use_id = "send1", content = "Queued for cloyd-gpt." } } } }),
         Entry(new { type = "user", message = new { role = "user", content = new[] { new { type = "tool_result", tool_use_id = "send2", is_error = true,
                      content = "The user doesn't want to proceed with this tool use." } } } })
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
