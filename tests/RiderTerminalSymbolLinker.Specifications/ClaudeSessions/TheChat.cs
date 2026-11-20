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

   /// <summary>Presses <paramref name="key"/>, <c>ENTER</c>, <c>SHIFT_ENTER</c>, <c>ESCAPE</c> or <c>SHIFT_TAB</c>, in the input box.</summary>
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

   /// <summary>Opens the conversation's fold <paramref name="fold"/>, by its place among the folds.</summary>
   public ChatSeen OpenTheFold(int fold) => ChatSeen.From(channel.OpenTheFoldInTheChat(tab, fold));

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
      public static object AssistantText(string id, string text) => new { kind = "assistant-text", id, text };

      /// <summary>A tool call, with its result, or none while it runs, when <paramref name="result"/> is null.</summary>
      public static object ToolCall(string id, string tool, IReadOnlyDictionary<string, string> input, string? result, bool isAnError = false) =>
         new { kind = "tool-call", id, tool, input, result = result is null ? null : new { text = result, isAnError } };

      public static object Compaction(string id) => new { kind = "compaction", id };
   }
}

/// <summary>
///   A tab's chat as the observation channel tells of it: the card the tab shows, <c>CHAT</c> or <c>TERMINAL</c>, or
///   null when the tab has no chat, and the card the user chose, or null when the tab chose it; the conversation's text,
///   its folds, the texts its links cover, and the id of the item at the top of the pane; the status line, and why
///   Claude Code didn't get what the chat last gave it, or null; the text in the input box, its parts and its caret;
///   and the titles of the cards' tabs.
/// </summary>
public sealed record ChatSeen(
   string? Card,
   string? CardTheUserChose,
   string Transcript,
   IReadOnlyList<FoldSeen> Folds,
   IReadOnlyList<string> Links,
   string? ItemAtTheTop,
   string? StatusLine,
   string? WhyNotTyped,
   string? MessageBox,
   IReadOnlyList<MessagePartSeen> MessageBoxParts,
   int Caret,
   IReadOnlyList<string> Tabs)
{
   public static ChatSeen From(JsonNode chat) =>
      chat["card"] is null
         ? new ChatSeen(null, null, "", [], [], null, null, null, null, [], 0, [])
         : new ChatSeen(chat["card"]!.GetValue<string>(),
                        chat["cardTheUserChose"]?.GetValue<string>(),
                        chat["transcript"]!.GetValue<string>(),
                        [.. chat["folds"]!.AsArray().Select(it => new FoldSeen(it!["header"]!.GetValue<string>(), it["folded"]!.GetValue<string>(), it["open"]!.GetValue<bool>()))],
                        [.. chat["links"]!.AsArray().Select(it => it!.GetValue<string>())],
                        chat["itemAtTheTop"]?.GetValue<string>(),
                        chat["statusLine"]!.GetValue<string>(),
                        chat["whyNotTyped"]?.GetValue<string>(),
                        chat["messageBox"]!.GetValue<string>(),
                        [.. chat["messageBoxParts"]!.AsArray().Select(it => MessagePartSeen.From(it!))],
                        chat["caret"]!.GetValue<int>(),
                        [.. chat["tabs"]!.AsArray().Select(it => it!.GetValue<string>())]);
}

/// <summary>A fold of the chat's conversation: the line it starts on, up to it, the text it folds away, and whether it's open.</summary>
public sealed record FoldSeen(string Header, string Folded, bool Open);

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
