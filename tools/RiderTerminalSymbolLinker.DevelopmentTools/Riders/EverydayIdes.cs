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
   ///   <br/>It also names the Marketplace as where the plugin gets its updates from, in the IDE's
   ///   <see cref="PluginUpdateSources"/>, where no source is named: a plugin installed from disk has none, and the IDE
   ///   warns about it as it starts.
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
            from line in DeployTo(ide, plugin, builds.BuildOf(plugin), new FolderMirror(setAside))
            select line
      ];
   }

   /// <summary>
   ///   Has the installed IDEs that <paramref name="plugin"/> <see cref="OurPlugin.RunsIn"/>, or only
   ///   <paramref name="onlyIn"/>, reload the build of it they have, as they would a new one: when Magnus next switches to
   ///   the IDE's window. Returns what it did, a line for each IDE.
   /// </summary>
   /// <remarks>
   ///   To see in Magnus's own IDEs what a reload does, such as whether his tabs come through it.
   ///   <br/>The next <see cref="Deploy"/> puts the build's own times back on its files, and so has the IDE reload it once
   ///   more, even if the build is the same.
   /// </remarks>
   public static IReadOnlyList<string> Reload(OurPlugin plugin, EverydayIde? onlyIn = null) =>
   [
      .. from ide in plugin.RunsIn
         where ide.IsInstalled && (onlyIn is null || ide == onlyIn)
         select ReloadIn(ide, plugin)
   ];

   static string ReloadIn(EverydayIde ide, OurPlugin plugin)
   {
      if(!Directory.Exists(Path.Combine(ide.PluginsFolder, plugin.Name))) return $"{ide} doesn't have {plugin}: deploy it first.";
      if(!ide.ReloadsChangedPlugins) return $"{ide} doesn't reload {plugin} without a restart: it doesn't run with -Didea.auto.reload.plugins=true.";
      plugin.MakeItLookChangedIn(ide.PluginsFolder);
      return $"{ide} reloads {plugin}, the build it has, when Magnus next switches to its window.";
   }

   static IEnumerable<string> DeployTo(EverydayIde ide, OurPlugin plugin, string build, FolderMirror mirror)
   {
      yield return MirrorTheBuild(ide, plugin, build, mirror);
      if(ide.PluginUpdateSources.NameTheMarketplaceUnlessASourceIsNamed(plugin))
         yield return $"{ide} gets updates of {plugin} from the Marketplace now, so it no longer warns, from its next start, that it can't tell where the plugin came from.";
   }

   static string MirrorTheBuild(EverydayIde ide, OurPlugin plugin, string build, FolderMirror mirror)
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
