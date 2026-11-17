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
   ///   Builds this working tree's plugin and puts it in the everyday plugins folder, in place of the one there. Returns
   ///   what it did.
   /// </summary>
   public string Deploy()
   {
      _plugins.Fill();
      var setAside = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiderTerminalSymbolLinkerDevelopment", "replaced-everyday-plugin-files");
      var mirror = new FolderMirror(setAside);
      var changed = mirror.Mirror(_plugins.Plugin, Path.Combine(_pluginsFolder, GradleSandbox.PluginFolderName));
      return !changed
                ? "The everyday Riders have this build already."
                : mirror.AddedOrRemovedAFile
                   ? "Deployed the build to the everyday Riders, adding or removing a file of the plugin's, which Rider can't reload: each loads it when Magnus restarts it."
                   : "Deployed the build to the everyday Riders: each reloads it, without a restart, when Magnus next switches to its window.";
   }
}
