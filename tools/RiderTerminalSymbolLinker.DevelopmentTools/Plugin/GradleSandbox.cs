using RiderTerminalSymbolLinker.DevelopmentTools.Processes;
using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

/// <summary>
///   The plugins folders that the Gradle build's <c>prepareSandbox</c> task fills, one for each of
///   <see cref="OurPlugin.All"/>: this working tree's build of the plugin, and the plugins it depends on, as the
///   Marketplace has them, such as the .NET Analysis&amp;Refactoring MCP the symbol linker depends on. The
///   <see cref="SandboxRider"/> installs our plugins from here, and the MCP's when it's to run the
///   <see cref="McpBuild.Marketplace"/> build.
/// </summary>
/// <remarks>
///   The Gradle build names each folder after its project, and the Rider it builds against, the installed one, as
///   <c>localIdePath</c> in the user's Gradle properties says.<br/>
///   The sandbox Rider doesn't load its plugins from here: a running Rider holds the MCP plugin's DLL open, and
///   <c>prepareSandbox</c> copies every file again, so Gradle couldn't build while it runs.
/// </remarks>
sealed class GradleSandbox
{
   /// <summary>The name of the .NET Analysis&amp;Refactoring MCP's folder in a plugins folder.</summary>
   public const string McpPluginFolderName = "DotNetAnalysisAndRefactoringMcp";

   readonly RiderInstallation _rider;

   public GradleSandbox(RiderInstallation rider) => _rider = rider;

   string PluginsFolderOf(OurPlugin plugin) =>
      Path.Combine(RepositoryRoot.Folder, ".intellijPlatform", "sandbox", plugin.Name, $"RD-{_rider.Version}", "plugins");

   /// <summary>This working tree's build of <paramref name="plugin"/>, once <see cref="Fill"/> has run.</summary>
   public string BuildOf(OurPlugin plugin) => Path.Combine(PluginsFolderOf(plugin), plugin.Name);

   /// <summary>The .NET Analysis&amp;Refactoring MCP's plugin, as the Marketplace has the version the symbol linker depends on.</summary>
   public string McpPlugin => Path.Combine(PluginsFolderOf(OurPlugin.SymbolLinker), McpPluginFolderName);

   /// <summary>Builds this working tree's plugins into their folders, with Gradle's <c>prepareSandbox</c>, which every project has.</summary>
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
