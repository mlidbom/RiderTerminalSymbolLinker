using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The Riders Magnus works in: the installed Rider with its everyday settings and plugins folder, which every Rider
///   window he opens shares.
/// </summary>
/// <remarks>
///   His Rider runs with <c>-Didea.auto.reload.plugins=true</c> (Magnus, 2026-11-17), so it reloads the plugin when its
///   files change, without a restart. It notices the change when it next looks at its plugins folder, which nothing
///   watches: when he switches to its window.
/// </remarks>
public sealed class EverydayRider
{
   readonly GradleSandbox _plugins;
   readonly string _pluginsFolder;

   public EverydayRider(RiderInstallation rider)
   {
      _plugins = new GradleSandbox(rider);
      _pluginsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JetBrains", rider.SettingsFolderName, "plugins");
   }

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

   /// <summary>
   ///   Builds this working tree's plugin and puts it in the everyday plugins folder, in place of the one there. Returns
   ///   what it did.
   /// </summary>
   public string Deploy()
   {
      _plugins.Fill();
      var setAside = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiderTerminalSymbolLinkerDevelopment", "replaced-everyday-plugin-files");
      var pluginFolder = Path.Combine(_pluginsFolder, GradleSandbox.PluginFolderName);
      var registeredBefore = PluginRegistrations.InPluginFolder(pluginFolder);
      var mirror = new FolderMirror(setAside);
      var changed = mirror.Mirror(_plugins.Plugin, pluginFolder);
      return !changed
                ? "The everyday Riders have this build already."
                : mirror.AddedOrRemovedAFile || PluginRegistrations.InPluginFolder(pluginFolder) != registeredBefore
                   ? "Deployed the build to the everyday Riders. It adds or removes a file of the plugin's, or changes what its plugin.xml registers, which a reload doesn't take: Magnus must restart each. A reload, which each does when he switches to it, would run the new classes with the old registrations."
                   : "Deployed the build to the everyday Riders: each reloads it, without a restart, when Magnus next switches to its window.";
   }
}
