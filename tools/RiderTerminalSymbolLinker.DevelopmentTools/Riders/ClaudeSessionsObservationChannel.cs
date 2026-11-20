namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   Claude Sessions' <see cref="ObservationChannel"/>, <c>dev.magnus.claudesessions.observation.ObservationChannel</c>:
///   the projects open, and the reloading of the plugin. What the plugin has Cloyd show, the sandbox's
///   <see cref="Cloyd.InstalledCloyd"/> tells.
/// </summary>
/// <remarks><see cref="ObservationChannel.State"/> is the projects open, each with its name and its folder, <c>basePath</c>.</remarks>
public sealed class ClaudeSessionsObservationChannel(int port) : ObservationChannel(port);
