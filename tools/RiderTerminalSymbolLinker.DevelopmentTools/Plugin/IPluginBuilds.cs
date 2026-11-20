namespace RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

/// <summary>
///   Where a build of each of <see cref="OurPlugin.All"/> is, for a deploy to take it from: the Gradle build's
///   <see cref="GradleSandbox"/>, or a copy put aside, <see cref="StagedPluginBuilds"/>.
/// </summary>
interface IPluginBuilds
{
   /// <summary>The build of <paramref name="plugin"/>: its folder, as an IDE's plugins folder holds the plugin.</summary>
   string BuildOf(OurPlugin plugin);
}
