using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CloydSpike.Hub;
using LiveMarkdown.Avalonia;

namespace CloydSpike.Chat;

/// <summary>
/// A session's conversation, as the hub's conversation stream tells it: Magnus's prompts, what Claude wrote as markdown,
/// its thinking, and its tool calls folded, each with its result inside.
/// </summary>
/// <remarks>
/// The spike shows the last <see cref="MessagesShown"/> messages, unvirtualized: a long conversation would need a
/// virtualizing list.
/// </remarks>
sealed class ConversationView : UserControl
{
   const int MessagesShown = 300;

   readonly StackPanel _messages = new() { Spacing = 8, Margin = new Thickness(8) };
   readonly ScrollViewer _scroller;
   readonly Dictionary<string, Control> _messagesById = [];
   readonly Dictionary<string, SelectableTextBlock> _resultsByToolUseId = [];
   CancellationTokenSource? _following;

   public ConversationView() => Content = _scroller = new ScrollViewer { Content = _messages, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

   /// <summary>How many messages it shows now.</summary>
   public int MessageCount => _messages.Children.Count;

   /// <summary>Shows the conversation of <paramref name="session"/>'s line from <paramref name="hub"/>, and follows it as it goes on, instead of what it showed.</summary>
   public void Follow(TheHub hub, RunningSession session)
   {
      _following?.Cancel();
      _following = new CancellationTokenSource();
      Clear();
      var cancellation = _following.Token;
      _ = Task.Run(async () =>
      {
         await foreach(var conversationEvent in hub.ConversationOf(session.SessionId, cancellation))
            Dispatcher.UIThread.Post(() => { if(!cancellation.IsCancellationRequested) Show(conversationEvent); });
      }, cancellation);
   }

   /// <summary>Shows what <paramref name="conversationEvent"/> tells: a message added, a tool's result, a rewind, or that the stream caught up.</summary>
   public void Show(EventOfAConversation conversationEvent)
   {
      var data = conversationEvent.Data;
      switch(conversationEvent.Name)
      {
         case "message":
            Add(data);
            break;
         case "tool-result":
            if(_resultsByToolUseId.TryGetValue((string)data["toolUseId"]!, out var result)) result.Text = (string?)data["text"];
            break;
         case "rewind":
            RewindTo((string?)data["lastMessageKept"]);
            break;
         case "caught-up":
            Dispatcher.UIThread.Post(() => _scroller.ScrollToEnd(), DispatcherPriority.Background);
            break;
      }
   }

   void Clear()
   {
      _messages.Children.Clear();
      _messagesById.Clear();
      _resultsByToolUseId.Clear();
   }

   void Add(JsonNode message)
   {
      var shown = ViewOf(message);
      shown.Tag = (string)message["id"]!;
      _messagesById[(string)message["id"]!] = shown;
      _messages.Children.Add(shown);
      if(_messages.Children.Count > MessagesShown) _messagesById.Remove((string)_messages.Children[0].Tag!);
      if(_messages.Children.Count > MessagesShown) _messages.Children.RemoveAt(0);
   }

   void RewindTo(string? lastMessageKept)
   {
      var keep = lastMessageKept is not null && _messagesById.TryGetValue(lastMessageKept, out var kept) ? _messages.Children.IndexOf(kept) + 1 : 0;
      foreach(var removed in _messages.Children.Skip(keep).ToList())
      {
         _messagesById.Remove((string)removed.Tag!);
         _messages.Children.Remove(removed);
      }
   }

   Control ViewOf(JsonNode message) =>
      (string)message["kind"]! switch
      {
         "promptOfMagnuss" => PromptOfMagnuss(message["parts"]!.AsArray()),
         "claudesText" => Markdown((string)message["text"]!),
         "thinking" => new SelectableTextBlock { Text = (string?)message["text"], FontStyle = FontStyle.Italic, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap },
         "toolCall" => ToolCall(message),
         "slashCommand" => Dim((string)message["asTyped"]!),
         "promptFromElsewhere" => Dim($"From {message["from"]}: {Shortened((string?)message["text"])}"),
         "interruption" => Dim((string)message["text"]!),
         "compaction" => Dim("The conversation was compacted."),
         "clearing" => Dim("/clear: the conversation goes on in a new session."),
         var kind => Dim(kind)
      };

   static Control PromptOfMagnuss(JsonArray parts) =>
      new Border
      {
         Background = new SolidColorBrush(Color.FromRgb(0x2b, 0x3a, 0x55)),
         CornerRadius = new CornerRadius(6),
         Padding = new Thickness(8, 6),
         HorizontalAlignment = HorizontalAlignment.Right,
         MaxWidth = 700,
         Child = new SelectableTextBlock
         {
            TextWrapping = TextWrapping.Wrap,
            Text = string.Concat(parts.Select(part => (string)part!["kind"]! == "pastedImage" ? "[Image]" : (string?)part["text"]))
         }
      };

   static Control Markdown(string text)
   {
      var markdown = new ObservableStringBuilder();
      markdown.Append(text);
      return new MarkdownRenderer { MarkdownBuilder = markdown };
   }

   Control ToolCall(JsonNode message)
   {
      var result = new SelectableTextBlock { FontFamily = new FontFamily("Cascadia Mono"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray };
      _resultsByToolUseId[(string)message["toolUseId"]!] = result;
      var input = message["input"]!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
      return new Expander
      {
         Header = $"⚙ {message["name"]}  {Shortened(SummaryOf(message["input"]!))}",
         HorizontalAlignment = HorizontalAlignment.Stretch,
         Content = new StackPanel
         {
            Spacing = 6,
            Children = { new SelectableTextBlock { Text = input, FontFamily = new FontFamily("Cascadia Mono"), FontSize = 12, TextWrapping = TextWrapping.Wrap }, result }
         }
      };
   }

   static string SummaryOf(JsonNode input) =>
      input is JsonObject fields && fields.FirstOrDefault().Value is JsonValue first ? first.ToString() : "";

   static string Shortened(string? text) => text is null ? "" : text.Length <= 120 ? text.ReplaceLineEndings(" ") : text[..120].ReplaceLineEndings(" ") + "…";

   static Control Dim(string text) => new TextBlock { Text = text, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };
}
