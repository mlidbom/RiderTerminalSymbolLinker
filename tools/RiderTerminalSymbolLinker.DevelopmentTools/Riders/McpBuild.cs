namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   Which build of the .NET Analysis&amp;Refactoring MCP the <see cref="SandboxRider"/> runs: its plugin, and the
///   <see cref="McpToolsBuilds"/> that plugin serves.
/// </summary>
public enum McpBuild
{
   /// <summary>
   ///   The build the <see cref="EverydayRider"/> runs: the MCP's own session deploys its working tree's builds there,
   ///   ahead of the Marketplace, so this is what Magnus runs, and what the MCP releases next.
   /// </summary>
   Everyday,

   /// <summary>
   ///   The Marketplace's build of the version the plugin's Gradle build depends on, with the tools builds it ships with:
   ///   what the plugin's users run.
   /// </summary>
   Marketplace
}
