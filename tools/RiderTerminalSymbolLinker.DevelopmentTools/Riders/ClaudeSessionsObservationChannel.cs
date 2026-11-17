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

   /// <summary>Types <paramref name="text"/> into the terminal of tab <paramref name="tab"/>, and Enter.</summary>
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
}
