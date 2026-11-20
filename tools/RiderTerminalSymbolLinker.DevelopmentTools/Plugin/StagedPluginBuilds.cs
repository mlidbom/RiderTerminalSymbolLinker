namespace RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

/// <summary>
///   Copies of this working tree's builds of the plugins, put aside in <paramref name="folder"/>, each in a folder of its
///   own named after the plugin: for a deploy that takes them later, while the working tree builds on, such as the one an
///   IDE's restarter runs while the IDE is down.
/// </summary>
sealed class StagedPluginBuilds(string folder) : IPluginBuilds
{
   public string BuildOf(OurPlugin plugin) => Path.Combine(folder, plugin.Name);

   /// <summary>Puts copies of the builds in <paramref name="builds"/> aside in <paramref name="folder"/>, in place of any there.</summary>
   public static StagedPluginBuilds StageFrom(IPluginBuilds builds, string folder)
   {
      var staged = new StagedPluginBuilds(folder);
      var setAside = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(folder))!, "replaced-staged-files");
      foreach(var plugin in OurPlugin.All) new FolderMirror(setAside).Mirror(builds.BuildOf(plugin), staged.BuildOf(plugin));
      return staged;
   }
}
