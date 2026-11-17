using RiderTerminalSymbolLinker.DevelopmentTools.Processes;
using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

/// <summary>
///   The plugins folder that the Gradle build's <c>prepareSandbox</c> task fills: this working tree's build of the
///   plugin, and the .NET Analysis&amp;Refactoring MCP it depends on, as the Marketplace has it. The
///   <see cref="SandboxRider"/> loads its plugins from here.
/// </summary>
/// <remarks>
///   The Gradle build names the folder after the Rider it builds against, the installed one, as <c>localIdePath</c> in
///   the user's Gradle properties says.
/// </remarks>
sealed class GradleSandbox
{
   const string PluginFolderName = "RiderTerminalSymbolLinker";
   const string McpPluginFolderName = "DotNetAnalysisAndRefactoringMcp";

   readonly RiderInstallation _rider;

   public GradleSandbox(RiderInstallation rider) => _rider = rider;

   public string PluginsFolder =>
      Path.Combine(RepositoryRoot.Folder, ".intellijPlatform", "sandbox", PluginFolderName, $"RD-{_rider.Version}", "plugins");

   /// <summary>Whether the folder has both plugins, so that a Rider loading its plugins from it can run.</summary>
   public bool HasBothPlugins => Directory.Exists(Path.Combine(PluginsFolder, PluginFolderName, "lib"))
                              && Directory.Exists(Path.Combine(PluginsFolder, McpPluginFolderName, "lib"));

   /// <summary>
   ///   The tools builds that the .NET Analysis&amp;Refactoring MCP ships with, which a Marketplace install serves,
   ///   instead of those the everyday Riders on this machine share.
   /// </summary>
   public string McpToolsFolder => Path.Combine(PluginsFolder, McpPluginFolderName, "tools");

   /// <summary>Builds this working tree's plugin into the folder, with Gradle's <c>prepareSandbox</c>.</summary>
   public void Fill() =>
      ExternalCommand.Run(Path.Combine(RepositoryRoot.Folder, "gradlew.bat"),
                          ["prepareSandbox", "--console=plain", "--quiet"],
                          RepositoryRoot.Folder,
                          new Dictionary<string, string> { ["JAVA_HOME"] = TheRepositorysJdk() });

   /// <summary>The JDK 21 the build needs, which the repository keeps under <c>.tooling/jdk</c>, outside git.</summary>
   static string TheRepositorysJdk() =>
      Directory.GetDirectories(Path.Combine(RepositoryRoot.Folder, ".tooling", "jdk"), "jdk-21*").SingleOrDefault()
   ?? throw new InvalidOperationException("No JDK 21 under .tooling/jdk: the build needs one there.");
}
