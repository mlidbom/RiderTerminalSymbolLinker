using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>The IDEs Magnus works in, each an <see cref="EverydayIde"/>, as the place our plugins are deployed to.</summary>
static class EverydayIdes
{
   /// <summary>
   ///   Builds this working tree's plugins and puts each in the plugins folder of every installed IDE it
   ///   <see cref="OurPlugin.RunsIn"/>, in place of the build there. Returns what it did, a line for each.
   /// </summary>
   /// <remarks>
   ///   An IDE with <see cref="EverydayIde.ReloadsChangedPlugins"/> reloads a plugin whose files changed when Magnus next
   ///   switches to its window. A build that adds or removes a file of the plugin's, or changes what its plugin.xml
   ///   registers, takes a restart all the same: a reload would run its classes with the registrations the IDE read when it
   ///   started. A plugin new to an IDE always does.
   /// </remarks>
   public static IReadOnlyList<string> Deploy(GradleSandbox builds)
   {
      builds.Fill();
      var setAside = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiderTerminalSymbolLinkerDevelopment", "replaced-everyday-plugin-files");
      return
      [
         .. from plugin in OurPlugin.All
            from ide in plugin.RunsIn
            where ide.IsInstalled
            select DeployTo(ide, plugin, builds.BuildOf(plugin), new FolderMirror(setAside))
      ];
   }

   static string DeployTo(EverydayIde ide, OurPlugin plugin, string build, FolderMirror mirror)
   {
      var pluginFolder = Path.Combine(ide.PluginsFolder, plugin.Name);
      var registeredBefore = PluginRegistrations.InPluginFolder(pluginFolder);
      if(!mirror.Mirror(build, pluginFolder)) return $"{ide} has this build of {plugin} already.";
      if(registeredBefore is null) return $"{ide} has {plugin} now, which it loads when Magnus next restarts it.";
      if(mirror.AddedOrRemovedAFile || PluginRegistrations.InPluginFolder(pluginFolder) != registeredBefore)
         return $"Deployed {plugin} to {ide}. The build adds or removes a file of the plugin's, or changes what its plugin.xml registers, which a reload doesn't take: Magnus must restart it.";
      return ide.ReloadsChangedPlugins
                ? $"Deployed {plugin} to {ide}, which reloads it, without a restart, when Magnus next switches to its window."
                : $"Deployed {plugin} to {ide}, which loads it when Magnus next restarts it: it doesn't run with -Didea.auto.reload.plugins=true.";
   }
}
