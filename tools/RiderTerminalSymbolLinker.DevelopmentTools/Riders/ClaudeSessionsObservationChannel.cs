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

   /// <summary>Starts a new Claude Code session in a tab, as the New Claude Session action does.</summary>
   public JsonNode StartANewSession(string? project = null) => Post("sessions/new", project, new { });

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
}
