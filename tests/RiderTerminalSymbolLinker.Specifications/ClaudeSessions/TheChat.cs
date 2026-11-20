using System.Diagnostics;
using System.Text.Json.Nodes;
using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

/// <summary>
///   The chat of a tab of the Claude Sessions view in the sandbox Rider, as the specifications see and drive it through
///   Claude Sessions' observation channel: its cards, its conversation, its status line and its input box, and the
///   stand-in behind it, <c>StandInBehindTheChat</c>, which gives the chat its conversation and Claude Code's status,
///   and takes what it sends, or refuses it, in place of the hub behind it, which doesn't tell of the conversation yet.
/// </summary>
public sealed class TheChat(ClaudeSessionsObservationChannel channel, int tab)
{
   /// <summary>How long the chat may take to show a change: it takes the stand-in's changes in on the EDT.</summary>
   static readonly TimeSpan HowLongAChangeMayTake = TimeSpan.FromSeconds(10);

   /// <summary>The chat as it is now.</summary>
   public ChatSeen Seen => ChatSeen.From(channel.TheChat(tab));

   /// <summary>Has the stand-in give the chat a conversation of <paramref name="items"/>, made by <see cref="Item"/>, and returns the chat once it shows it.</summary>
   public ChatSeen Give(params object[] items)
   {
      channel.GiveTheChatAConversation(tab, new { items });
      return SeenOnce(it => items.Length == 0 || it.Transcript.Length > 0, "the chat to show the conversation");
   }

   /// <summary>Has the stand-in say Claude Code's status is <paramref name="status"/>: <c>NOT_RUNNING</c>, <c>IDLE</c>, <c>BUSY</c> or <c>WAITING</c>.</summary>
   public ChatSeen SayTheStatus(string status) => ChatSeen.From(channel.SayTheStatusToTheChat(tab, status));

   /// <summary>Types <paramref name="text"/> into the input box, at its caret.</summary>
   public ChatSeen Type(string text) => ChatSeen.From(channel.TypeInTheChat(tab, text));

   /// <summary>Pastes <paramref name="text"/> into the input box, as from the clipboard.</summary>
   public ChatSeen Paste(string text) => ChatSeen.From(channel.PasteInTheChat(tab, text));

   /// <summary>Pastes a small image into the input box, as from the clipboard.</summary>
   public ChatSeen PasteAnImage() => ChatSeen.From(channel.PasteAnImageInTheChat(tab));

   /// <summary>Clicks the input box's send button: it sends, stops Claude, or does nothing, as it shows.</summary>
   public ChatSeen ClickTheSendButton() => ChatSeen.From(channel.ClickTheSendButtonInTheChat(tab));

   /// <summary>Clicks the thumbnail <paramref name="at"/>, counting from 0, over the input box, and returns the file the IDE's editor then shows.</summary>
   public string? ClickTheThumbnail(int at) => channel.ClickTheThumbnailInTheChat(tab, at)["selectedEditorFile"]?.GetValue<string>();

   /// <summary>Presses <paramref name="key"/>, <c>ENTER</c>, <c>CTRL_ENTER</c>, <c>SHIFT_ENTER</c>, <c>ESCAPE</c>, <c>SHIFT_TAB</c> or <c>BACKSPACE</c>, in the input box.</summary>
   public ChatSeen Press(string key) => ChatSeen.From(channel.PressInTheChat(tab, key));

   /// <summary>Switches between the chat and the terminal, as the action does from Find Action.</summary>
   public ChatSeen Switch() => ChatSeen.From(channel.SwitchBetweenTheChatAndTheTerminal(tab));

   /// <summary>Shows the card <paramref name="card"/>, <c>CHAT</c> or <c>TERMINAL</c>, as clicking its tab does.</summary>
   public ChatSeen ClickTheTabOf(string card) => ChatSeen.From(channel.ClickTheTabOfTheCard(tab, card));

   /// <summary>
   ///   The id of the action the IDE would run for <paramref name="keystroke"/>, such as <c>ctrl H</c>, pressed in the
   ///   <c>chat</c> or the <c>terminal</c>, or <c>elsewhere</c> in the IDE's window, or null when it would run none.
   /// </summary>
   public string? TheActionTheShortcutRuns(string keystroke, string pressedIn) =>
      channel.TheActionTheShortcutRuns(tab, keystroke, pressedIn)["action"]?.GetValue<string>();

   /// <summary>Clicks the line of the transcript's row <paramref name="row"/>, by its key: opens a prompt from elsewhere, a tool call or a run of them, or closes it.</summary>
   public ChatSeen ClickTheRow(string row) => ChatSeen.From(channel.ClickTheRowInTheChat(tab, row));

   /// <summary>Opens the transcript's find bar, and has it seek <paramref name="text"/>, as the user types it.</summary>
   public ChatSeen Find(string text) => ChatSeen.From(channel.FindInTheChat(tab, text));

   /// <summary>Goes to the next place the find bar found, as Enter in its field does.</summary>
   public ChatSeen FindNext() => ChatSeen.From(channel.FindNextInTheChat(tab));

   /// <summary>Goes to the place before, as Shift+Enter in the find bar's field does.</summary>
   public ChatSeen FindPrevious() => ChatSeen.From(channel.FindNextInTheChat(tab, forward: false));

   /// <summary>Closes the find bar, as Escape in its field does.</summary>
   public ChatSeen CloseTheFind() => ChatSeen.From(channel.CloseTheFindInTheChat(tab));

   /// <summary>Scrolls the conversation so that the item <paramref name="item"/>, by its id, is at the top, as the user would.</summary>
   public ChatSeen ScrollTheTranscriptTo(string item) => ChatSeen.From(channel.ScrollTheTranscriptInTheChat(tab, item));

   /// <summary>Has the stand-in refuse what the chat gives it from then on, nothing of it typed, for <paramref name="why"/>, shown in the terminal when <paramref name="theTerminalShowsWhy"/>.</summary>
   public ChatSeen HaveClaudeCodeGetNothingOfIt(string why, bool theTerminalShowsWhy) =>
      ChatSeen.From(channel.AnswerWhatTheChatTypesWith(tab, new { outcome = "nothingTyped", why, theTerminalShowsWhy }));

   /// <summary>Has the stand-in leave what the chat gives it from then on unsent in Claude Code's own input box, for <paramref name="why"/>.</summary>
   public ChatSeen HaveClaudeCodeLeaveItUnsent(string why) => ChatSeen.From(channel.AnswerWhatTheChatTypesWith(tab, new { outcome = "leftUnsentInTheTerminal", why }));

   /// <summary>Has the stand-in take what the chat gives it from then on, as Claude Code does.</summary>
   public ChatSeen HaveClaudeCodeGetIt() => ChatSeen.From(channel.AnswerWhatTheChatTypesWith(tab, new { outcome = "typed" }));

   /// <summary>Has the stand-in hold what the chat gives it until <see cref="LetGo"/>, as typing into Claude Code waits while other typing has the turn.</summary>
   public ChatSeen Hold() => ChatSeen.From(channel.HoldWhatTheChatTypes(tab, hold: true));

   /// <summary>Has the stand-in let go of what it held.</summary>
   public ChatSeen LetGo() => ChatSeen.From(channel.HoldWhatTheChatTypes(tab, hold: false));

   /// <summary>What Claude Code got from the chat, through the stand-in: its messages, and the keys it passed on.</summary>
   public SentToClaudeCode Sent => SentToClaudeCode.From(channel.WhatTheChatSent(tab));

   /// <summary>What Claude Code got from the chat once <paramref name="condition"/> holds of it: the chat gives it what it was given in turn, on the EDT.</summary>
   public SentToClaudeCode SentOnce(Func<SentToClaudeCode, bool> condition, string whatItWaitsFor)
   {
      var stopwatch = Stopwatch.StartNew();
      while(true)
      {
         var sent = Sent;
         if(condition(sent)) return sent;
         if(stopwatch.Elapsed > HowLongAChangeMayTake) throw new TimeoutException($"Gave up after {HowLongAChangeMayTake} waiting for {whatItWaitsFor}. Sent: {ObservationChannel.Indent(channel.WhatTheChatSent(tab))}");
         Thread.Sleep(100);
      }
   }

   /// <summary>The chat once <paramref name="condition"/> holds of it, as it takes a change in.</summary>
   public ChatSeen SeenOnce(Func<ChatSeen, bool> condition, string whatItWaitsFor)
   {
      var stopwatch = Stopwatch.StartNew();
      while(true)
      {
         var seen = Seen;
         if(condition(seen)) return seen;
         if(stopwatch.Elapsed > HowLongAChangeMayTake) throw new TimeoutException($"Gave up after {HowLongAChangeMayTake} waiting for {whatItWaitsFor}. The chat: {ObservationChannel.Indent(channel.TheChat(tab))}");
         Thread.Sleep(100);
      }
   }

   /// <summary>The items of a conversation the stand-in gives the chat, as the plugin's observation channel reads them.</summary>
   public static class Item
   {
      public static object Prompt(string id, string text) => new { kind = "prompt", id, text };

      /// <summary>A message from the session named <paramref name="name"/>, as Claude Code's <c>SendMessage</c> brings it.</summary>
      public static object FromAnotherSession(string id, string name, string text) => new { kind = "from-another-session", id, name, text };

      /// <summary>Claude Code's notice that a background task finished, in its own words, <paramref name="text"/>.</summary>
      public static object FromABackgroundTask(string id, string text) => new { kind = "from-a-background-task", id, text };

      public static object AssistantText(string id, string text) => new { kind = "assistant-text", id, text };
      public static object Thinking(string id, string text) => new { kind = "thinking", id, text };

      /// <summary>A tool call, with its result, or none while it runs, when <paramref name="result"/> is null.</summary>
      public static object ToolCall(string id, string tool, IReadOnlyDictionary<string, string> input, string? result, bool isAnError = false) =>
         new { kind = "tool-call", id, tool, input, result = result is null ? null : new { text = result, isAnError } };

      public static object Compaction(string id) => new { kind = "compaction", id };
   }
}

/// <summary>
///   A tab's chat as the observation channel tells of it: the card the tab shows, <c>CHAT</c> or <c>TERMINAL</c>, or
///   null when the tab has no chat, and the card the user chose, or null when the tab chose it; the transcript's rows, the
///   texts the console filters' links cover, the id of the item at the top of the pane, and the transcript's find bar;
///   the working line, or null while it doesn't show; the status line, and why Claude Code didn't get what the chat last gave it, or null; the text in the input box, its
///   parts, its caret, how many lines tall it is, and how many it has room for: two fifths of the chat's height, which
///   in the sandbox Rider is its tool window's; the thumbnails over it, and what its send button does, <c>SEND</c>,
///   <c>STOP</c> or <c>NOTHING</c>; and the titles of the cards' tabs.
/// </summary>
public sealed record ChatSeen(
   string? Card,
   string? CardTheUserChose,
   IReadOnlyList<RowSeen> Rows,
   IReadOnlyList<string> Links,
   string? ItemAtTheTop,
   FindSeen? Find,
   string? WorkingLine,
   string? StatusLine,
   string? WhyNotTyped,
   string? MessageBox,
   IReadOnlyList<MessagePartSeen> MessageBoxParts,
   int Caret,
   int MessageBoxLines,
   int MessageBoxRoom,
   IReadOnlyList<ThumbnailSeen> Thumbnails,
   string? SendButton,
   IReadOnlyList<string> Tabs)
{
   /// <summary>The rows' texts, each on a line of its own, as they read on the screen: the bodies of tool calls aside.</summary>
   public string Transcript => string.Join("\n", Rows.Select(it => it.Text));

   /// <summary>The row with the key <paramref name="key"/>.</summary>
   public RowSeen Row(string key) => Rows.Single(it => it.Key == key);

   public static ChatSeen From(JsonNode chat) =>
      chat["card"] is null
         ? new ChatSeen(null, null, [], [], null, null, null, null, null, null, [], 0, 0, 0, [], null, [])
         : new ChatSeen(chat["card"]!.GetValue<string>(),
                        chat["cardTheUserChose"]?.GetValue<string>(),
                        [.. chat["rows"]!.AsArray().Select(it => RowSeen.From(it!))],
                        [.. chat["links"]!.AsArray().Select(it => it!.GetValue<string>())],
                        chat["itemAtTheTop"]?.GetValue<string>(),
                        FindSeen.From(chat["find"]!),
                        chat["workingLine"]?.GetValue<string>(),
                        chat["statusLine"]!.GetValue<string>(),
                        chat["whyNotTyped"]?.GetValue<string>(),
                        chat["messageBox"]!.GetValue<string>(),
                        [.. chat["messageBoxParts"]!.AsArray().Select(it => MessagePartSeen.From(it!))],
                        chat["caret"]!.GetValue<int>(),
                        chat["messageBoxLines"]!.GetValue<int>(),
                        chat["messageBoxRoom"]!.GetValue<int>(),
                        [.. chat["thumbnails"]!.AsArray().Select(it => ThumbnailSeen.From(it!))],
                        chat["sendButton"]!.GetValue<string>(),
                        [.. chat["tabs"]!.AsArray().Select(it => it!.GetValue<string>())]);
}

/// <summary>A thumbnail over the chat's input box, of an image it holds: its caption, the image's place-holder, and the image's file.</summary>
public sealed record ThumbnailSeen(string Caption, string File)
{
   public static ThumbnailSeen From(JsonNode thumbnail) => new(thumbnail["caption"]!.GetValue<string>(), thumbnail["file"]!.GetValue<string>());
}

/// <summary>
///   A row of the chat's transcript: its key; its kind, <c>prompt</c>, <c>from</c> (a prompt from elsewhere, with its
///   caption), <c>claude</c>, <c>thinking</c>, <c>interruption</c>, <c>compaction</c>, <c>clearing</c>, <c>tool-call</c>
///   or <c>tool-calls</c> (the line of a run of them); and its text, as it reads on the screen. A prompt from elsewhere,
///   and a tool call, have their body, shown or not, and say whether they're open; a tool call says whether it's in a
///   run too; a run says whether it's open.
/// </summary>
public sealed record RowSeen(string Key, string Kind, string Text, string? Caption, string? Body, bool Open, bool InARun)
{
   public static RowSeen From(JsonNode row) =>
      new(row["key"]!.GetValue<string>(),
          row["kind"]!.GetValue<string>(),
          row["text"]!.GetValue<string>(),
          row["caption"]?.GetValue<string>(),
          row["body"]?.GetValue<string>(),
          row["open"]?.GetValue<bool>() ?? false,
          row["inARun"]?.GetValue<bool>() ?? false);
}

/// <summary>
///   The transcript's find bar: whether it's shown, the text in its field, what it says of how many places hold it, such
///   as <c>3/17</c>, and the place the user is at, by its item and the index of the item's text, or nulls.
/// </summary>
public sealed record FindSeen(bool Shown, string Text, string Count, string? CurrentItem, int? CurrentPart)
{
   public static FindSeen From(JsonNode find) =>
      new(find["shown"]!.GetValue<bool>(),
          find["text"]!.GetValue<string>(),
          find["count"]!.GetValue<string>(),
          find["current"]?["item"]?.GetValue<string>(),
          find["current"]?["part"]?.GetValue<int>());
}

/// <summary>What Claude Code got from a tab's chat, through the stand-in behind it: its messages, and how often it was interrupted and its permission mode changed.</summary>
public sealed record SentToClaudeCode(IReadOnlyList<IReadOnlyList<MessagePartSeen>> Messages, int Interruptions, int PermissionModeChanges)
{
   public static SentToClaudeCode From(JsonNode sent) =>
      new([.. sent["messages"]!.AsArray().Select(message => (IReadOnlyList<MessagePartSeen>)[.. message!["parts"]!.AsArray().Select(it => MessagePartSeen.From(it!))])],
          sent["interruptions"]!.GetValue<int>(),
          sent["permissionModeChanges"]!.GetValue<int>());
}

/// <summary>
///   A part of a message the chat sent: its kind, <c>typed</c>, <c>pasted</c> or <c>image</c>, and its text; for an
///   image, its file, and whether the file is there.
/// </summary>
public sealed record MessagePartSeen(string Kind, string Text, string? File = null, bool FileExists = false)
{
   public static MessagePartSeen From(JsonNode part) =>
      new(part["kind"]!.GetValue<string>(), part["text"]!.GetValue<string>(), part["file"]?.GetValue<string>(), part["fileExists"]?.GetValue<bool>() ?? false);
}
