using RiderTerminalSymbolLinker.DevelopmentTools.Processes;
using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

/// <summary>
///   The plugins folder that the Gradle build's <c>prepareSandbox</c> task fills: this working tree's build of the
///   plugin, and the .NET Analysis&amp;Refactoring MCP it depends on, as the Marketplace has it. The
///   <see cref="SandboxRider"/> installs its plugins from here.
/// </summary>
/// <remarks>
///   The Gradle build names the folder after the Rider it builds against, the installed one, as <c>localIdePath</c> in
///   the user's Gradle properties says.<br/>
///   The sandbox Rider doesn't load its plugins from here: a running Rider holds the MCP plugin's DLL open, and
///   <c>prepareSandbox</c> copies every file again, so Gradle couldn't build while it runs.
/// </remarks>
sealed class GradleSandbox
{
   /// <summary>The name of the plugin's folder in a plugins folder.</summary>
   public const string PluginFolderName = "RiderTerminalSymbolLinker";

   /// <summary>The name of the .NET Analysis&amp;Refactoring MCP's folder in a plugins folder.</summary>
   public const string McpPluginFolderName = "DotNetAnalysisAndRefactoringMcp";

   readonly RiderInstallation _rider;

   public GradleSandbox(RiderInstallation rider) => _rider = rider;

   public string PluginsFolder =>
      Path.Combine(RepositoryRoot.Folder, ".intellijPlatform", "sandbox", PluginFolderName, $"RD-{_rider.Version}", "plugins");

   /// <summary>This working tree's build of the plugin, once <see cref="Fill"/> has run.</summary>
   public string Plugin => Path.Combine(PluginsFolder, PluginFolderName);

   /// <summary>The .NET Analysis&amp;Refactoring MCP's plugin, as the Marketplace has the version the build depends on.</summary>
   public string McpPlugin => Path.Combine(PluginsFolder, McpPluginFolderName);

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
