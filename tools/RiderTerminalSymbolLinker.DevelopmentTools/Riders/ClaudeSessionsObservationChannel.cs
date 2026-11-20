using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   Claude Sessions' <see cref="ObservationChannel"/>, <c>dev.magnus.claudesessions.observation.ObservationChannel</c>:
///   the projects open, and the reloading of the plugin. What the plugin has Cloyd show, the sandbox's
///   <see cref="Cloyd.InstalledCloyd"/> tells.
/// </summary>
/// <remarks><see cref="ObservationChannel.State"/> is the projects open, each with its name and its folder, <c>basePath</c>.</remarks>
public sealed class ClaudeSessionsObservationChannel(int port) : ObservationChannel(port)
{
   /// <summary>The id of Tools &gt; Show the Project in Cloyd, as Claude Sessions' plugin.xml registers it.</summary>
   public const string ShowTheProjectInCloyd = "dev.magnus.claudesessions.ShowTheProjectInCloyd";

   /// <summary>
   ///   Performs the IDE's action <paramref name="actionId"/> in the project open, as the user would from a menu or by its
   ///   shortcut: returns once it has been performed, while what it starts in the background may go on.
   /// </summary>
   public JsonNode Perform(string actionId) => Post("actions/perform", null, new { }, "id=" + Uri.EscapeDataString(actionId));
}
