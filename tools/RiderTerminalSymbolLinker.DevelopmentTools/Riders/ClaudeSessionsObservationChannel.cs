using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   Claude Sessions' <see cref="ObservationChannel"/>, <c>dev.magnus.claudesessions.observation.ObservationChannel</c>:
///   the tabs of its tool window, the Claude Code session each runs, and the sessions it has saved.
/// </summary>
/// <remarks>
///   <see cref="ObservationChannel.State"/> is each open project's view: its tabs, the process each runs and the session
///   in it, the saved sessions, and whether they're kept current.
/// </remarks>
public sealed class ClaudeSessionsObservationChannel(int port) : ObservationChannel(port)
{
   /// <summary>Has the IDE give the Claude Sessions tool window its content, as showing it would, and tells its tabs.</summary>
   public JsonNode OpenTheView(string? project = null) => Post("view/open", project, new { });

   /// <summary>Starts a new Claude Code session in a tab, without a name, as the New Claude Session popup does with its field left empty.</summary>
   public JsonNode StartANewSession(string? project = null) => Post("sessions/new", project, new { });

   /// <summary>Opens the New Claude Session popup, as the New Claude Session action does, and answers as <see cref="TheNewSessionPopup"/>.</summary>
   public JsonNode OpenTheNewSessionPopup(string? project = null) => Post("new-session-popup/open", project, new { });

   /// <summary>Whether the New Claude Session popup shows in the project's window, and the name typed into it.</summary>
   public JsonNode TheNewSessionPopup(string? project = null) => Get("new-session-popup", project);

   /// <summary>Types <paramref name="text"/> into the New Claude Session popup's field, for the name.</summary>
   public JsonNode NameInTheNewSessionPopup(string text, string? project = null) => Post("new-session-popup/name", project, new { text });

   /// <summary>
   ///   Has the New Claude Session popup's field take Enter, which starts the session, and answers with the place of its
   ///   tab among the view's tabs.
   /// </summary>
   public JsonNode PressEnterInTheNewSessionPopup(string? project = null) => Post("new-session-popup/enter", project, new { });

   /// <summary>Closes the New Claude Session popup, as Escape does.</summary>
   public JsonNode CloseTheNewSessionPopup(string? project = null) => Post("new-session-popup/close", project, new { });

   /// <summary>
   ///   Has the project's terminals run the shell <paramref name="path"/> from now on, such as <c>cmd.exe</c>, as its
   ///   setting in the IDE's terminal settings does, and answers with the one they ran before.
   /// </summary>
   public JsonNode RunTheShell(string path, string? project = null) => Post("terminal/shell", project, new { }, $"path={Uri.EscapeDataString(path)}");

   /// <summary>The text the terminal of tab <paramref name="tab"/> shows, from the top of its screen.</summary>
   public JsonNode Screen(int tab, string? project = null) => Get("tabs/screen", project, $"tab={tab}");

   /// <summary>Opens a plain terminal tab in the view's tool window, not through the view, and answers with its place among the view's tabs.</summary>
   public JsonNode OpenAPlainTerminalTab(string? project = null) => Post("tabs/new-plain", project, new { });

   /// <summary>Selects tab <paramref name="tab"/>, and shows the tool window, as a click on the tab does.</summary>
   public JsonNode Select(int tab, string? project = null) => Post("tabs/select", project, new { }, $"tab={tab}");

   /// <summary>Types <paramref name="text"/> into the tab's terminal, and Enter.</summary>
   public JsonNode Type(int tab, string text, string? project = null) => Post("tabs/type", project, new { text }, $"tab={tab}");

   /// <summary>Closes every tab by ending its process, as Claude Code's exit does, so that a specification starts with none.</summary>
   public JsonNode CloseEveryTab(string? project = null) => Post("tabs/close-all", project, new { });

   /// <summary>
   ///   Closes tab <paramref name="tab"/> by ending its process, as Claude Code's exit does: its close button asks the user
   ///   to confirm, and that dialog would wait for a human.
   /// </summary>
   public JsonNode Close(int tab, string? project = null) => Post("tabs/close", project, new { }, $"tab={tab}");

   /// <summary>Opens the session history picker, as the Resume a Claude Session action does, and answers as <see cref="ThePicker"/> once it shows.</summary>
   public JsonNode OpenThePicker(string? project = null) => Post("history/open", project, new { });

   /// <summary>
   ///   The session history picker shown in the project's window: its scope, the scopes it offers, its filter, its entries,
   ///   each with where its session is and whether it can be chosen, and the one selected.
   /// </summary>
   public JsonNode ThePicker(string? project = null) => Get("history", project);

   /// <summary>Types <paramref name="text"/> into the picker's search field, which filters its entries.</summary>
   public JsonNode FilterThePicker(string text, string? project = null) => Post("history/filter", project, new { text });

   /// <summary>Has the picker list the scope named <paramref name="scope"/>, such as <c>ALL_PROJECTS</c>, as a click on its button does.</summary>
   public JsonNode ListInThePicker(string scope, string? project = null) => Post("history/scope", project, new { }, $"scope={scope}");

   /// <summary>Selects the picker's entry <paramref name="entry"/>, by its place, and chooses it, as Enter does.</summary>
   public JsonNode ChooseInThePicker(int entry, string? project = null) => Post("history/choose", project, new { }, $"entry={entry}");

   /// <summary>Closes the picker, choosing nothing, as Escape does.</summary>
   public JsonNode CloseThePicker(string? project = null) => Post("history/close", project, new { });

   /// <summary>Has the picker's search field take the key <paramref name="key"/>, <c>UP</c>, <c>DOWN</c> or <c>ENTER</c>, as from the keyboard.</summary>
   public JsonNode PressInThePicker(string key, string? project = null) => Post("history/press", project, new { }, $"key={key}");

   /// <summary>Double-clicks the picker's entry <paramref name="entry"/>, by its place.</summary>
   public JsonNode DoubleClickInThePicker(int entry, string? project = null) => Post("history/double-click", project, new { }, $"entry={entry}");

   /// <summary>The actions the sandbox's active keymap binds <paramref name="keystroke"/> to, such as <c>ctrl alt shift H</c>.</summary>
   public JsonNode ActionsBoundTo(string keystroke) => Get("keymap", null, $"keystroke={Uri.EscapeDataString(keystroke)}");

   /// <summary>
   ///   The actions the keymap named <paramref name="keymap"/>, such as <c>Visual Studio 2022</c>, binds
   ///   <paramref name="keystroke"/> to.
   /// </summary>
   public JsonNode ActionsBoundTo(string keystroke, string keymap) =>
      Get("keymap", null, $"keystroke={Uri.EscapeDataString(keystroke)}&keymap={Uri.EscapeDataString(keymap)}");

   /// <summary>
   ///   The chat of tab <paramref name="tab"/>: the card shown, or null when the tab has no chat; the conversation's text,
   ///   its folds, each with its header line, its folded text and whether it's open, and the texts its links cover; the
   ///   status line; the text in the input box; and the switch button's text. Every chat call answers so.
   /// </summary>
   public JsonNode TheChat(int tab, string? project = null) => Get("tabs/chat", project, $"tab={tab}");

   /// <summary>
   ///   Has the stand-in behind the chat of tab <paramref name="tab"/> give it a conversation: <c>{"items": [...]}</c>,
   ///   each item with its <c>kind</c> and <c>id</c>, as <c>TheChatObserved.conversationFrom</c> in the plugin reads it.
   /// </summary>
   public JsonNode GiveTheChatAConversation(int tab, object conversation, string? project = null) => Post("tabs/chat/conversation", project, conversation, $"tab={tab}");

   /// <summary>Has the stand-in behind the chat say Claude Code's status is <paramref name="status"/>: <c>NOT_RUNNING</c>, <c>IDLE</c>, <c>BUSY</c> or <c>WAITING</c>.</summary>
   public JsonNode SayTheStatusToTheChat(int tab, string status, string? project = null) => Post("tabs/chat/status", project, new { }, $"tab={tab}&status={status}");

   /// <summary>What the chat sent the stand-in behind it: its messages, part by part, and how often it interrupted and changed the permission mode.</summary>
   public JsonNode WhatTheChatSent(int tab, string? project = null) => Get("tabs/chat/sent", project, $"tab={tab}");

   /// <summary>Types <paramref name="text"/> into the chat's input box, at its caret.</summary>
   public JsonNode TypeInTheChat(int tab, string text, string? project = null) => Post("tabs/chat/type", project, new { text }, $"tab={tab}");

   /// <summary>Pastes <paramref name="text"/> into the chat's input box, as from the clipboard, though not through the system's.</summary>
   public JsonNode PasteInTheChat(int tab, string text, string? project = null) => Post("tabs/chat/paste", project, new { text }, $"tab={tab}");

   /// <summary>Pastes a small image into the chat's input box, as from the clipboard.</summary>
   public JsonNode PasteAnImageInTheChat(int tab, string? project = null) => Post("tabs/chat/paste-image", project, new { }, $"tab={tab}");

   /// <summary>Presses <paramref name="key"/>, <c>ENTER</c>, <c>SHIFT_ENTER</c>, <c>ESCAPE</c> or <c>SHIFT_TAB</c>, in the chat's input box.</summary>
   public JsonNode PressInTheChat(int tab, string key, string? project = null) => Post("tabs/chat/press", project, new { }, $"tab={tab}&key={key}");

   /// <summary>Switches tab <paramref name="tab"/> between its chat and its terminal, as the button in the tool window's title bar does.</summary>
   public JsonNode SwitchBetweenTheChatAndTheTerminal(int tab, string? project = null) => Post("tabs/chat/switch", project, new { }, $"tab={tab}");

   /// <summary>Opens the conversation's fold <paramref name="fold"/>, by its place, or closes it when not <paramref name="open"/>.</summary>
   public JsonNode OpenTheFoldInTheChat(int tab, int fold, bool open = true, string? project = null) =>
      Post("tabs/chat/fold", project, new { }, $"tab={tab}&fold={fold}&open={(open ? "true" : "false")}");
}
