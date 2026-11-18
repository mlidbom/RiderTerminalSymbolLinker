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
///   <c>localIdePath</c> in the user's Gradle properties says, by its own reading of that Rider's version: Rider
///   2026.3.0.1's folder is <c>RD-2026.3</c>, while the earlier 2026.1.2's was <c>RD-2026.1.2</c>. So the folder is
///   found as the one that holds the plugin's newest build, not named here.<br/>
///   The sandbox Rider doesn't load its plugins from here: a running Rider holds the MCP plugin's DLL open, and
///   <c>prepareSandbox</c> copies every file again, so Gradle couldn't build while it runs.
/// </remarks>
sealed class GradleSandbox
{
   /// <summary>The name of the .NET Analysis&amp;Refactoring MCP's folder in a plugins folder.</summary>
   public const string McpPluginFolderName = "DotNetAnalysisAndRefactoringMcp";

   /// <summary>The plugins folder that holds the newest build of <paramref name="plugin"/>: the one <see cref="Fill"/> fills.</summary>
   static string PluginsFolderOf(OurPlugin plugin) =>
      Directory.GetDirectories(Path.Combine(RepositoryRoot.Folder, ".intellijPlatform", "sandbox", plugin.Name), "RD-*")
               .Select(it => Path.Combine(it, "plugins"))
               .Where(it => Directory.Exists(Path.Combine(it, plugin.Name)))
               .MaxBy(it => Directory.GetFiles(Path.Combine(it, plugin.Name), "*", SearchOption.AllDirectories).Max(File.GetLastWriteTimeUtc))
   ?? throw new InvalidOperationException($"Gradle's prepareSandbox hasn't built {plugin} yet.");

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
