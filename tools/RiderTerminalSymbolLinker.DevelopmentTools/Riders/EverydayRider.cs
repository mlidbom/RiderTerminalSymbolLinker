using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The Riders Magnus works in, the <see cref="EverydayIde.Rider"/>, as the source of the .NET Analysis&amp;Refactoring
///   MCP build that the <see cref="SandboxRider"/> runs by default: the MCP's own session deploys its builds there.
/// </summary>
public sealed class EverydayRider
{
   readonly string _pluginsFolder;

   public EverydayRider(RiderInstallation rider) =>
      _pluginsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JetBrains", rider.SettingsFolderName, "plugins");

   /// <summary>
   ///   The .NET Analysis&amp;Refactoring MCP's plugin that these Riders run: the MCP's own session deploys its working
   ///   tree's builds here.
   /// </summary>
   public string McpPlugin => Path.Combine(_pluginsFolder, GradleSandbox.McpPluginFolderName);

   /// <summary>
   ///   The tools builds that these Riders' <see cref="McpPlugin"/> serves the newest of: the MCP's own session deploys
   ///   its working tree's builds here, and every everyday Rider takes a new one up at its next call.
   /// </summary>
   public McpToolsBuilds McpToolsBuilds { get; } =
      new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotNetAnalysisAndRefactoringMcp", "tools"));
}
