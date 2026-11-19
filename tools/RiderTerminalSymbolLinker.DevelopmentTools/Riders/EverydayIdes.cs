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
   ///   started. So such a build isn't put into an IDE that runs and reloads changed plugins, as it would reload it at once,
   ///   broken: Magnus closes the IDE, and the deploy is run again. A plugin new to an IDE always takes a restart.
   ///   <br/>It also names the Marketplace as where the plugin gets its updates from, in the IDE's
   ///   <see cref="PluginUpdateSources"/>, where no source is named: a plugin installed from disk has none, and the IDE
   ///   warns about it as it starts.
   ///   <br/>With <paramref name="dryRun"/>, it builds the plugins and says what deploying them would do, changing
   ///   nothing in the IDEs: a deploy that takes a restart waits for Magnus's go-ahead.
   ///   <br/>With <paramref name="intoRunningIdes"/>, it puts such a build into an IDE that runs and reloads too, for
   ///   Magnus to restart it right after: the plugin is broken in it until he does. The restart loads the build, and
   ///   Claude Sessions brings back the tabs it saved.
   /// </remarks>
   public static IReadOnlyList<string> Deploy(GradleSandbox builds, bool dryRun = false, bool intoRunningIdes = false)
   {
      builds.Fill();
      var setAside = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiderTerminalSymbolLinkerDevelopment", "replaced-everyday-plugin-files");
      return
      [
         .. from plugin in OurPlugin.All
            from ide in plugin.RunsIn
            where ide.IsInstalled
            from line in DeployTo(ide, plugin, builds.BuildOf(plugin), new FolderMirror(setAside), dryRun, intoRunningIdes)
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

   static IEnumerable<string> DeployTo(EverydayIde ide, OurPlugin plugin, string build, FolderMirror mirror, bool dryRun, bool intoRunningIdes)
   {
      var pluginFolder = Path.Combine(ide.PluginsFolder, plugin.Name);
      var effect = EffectOfDeploying(ide, build, pluginFolder);
      if(dryRun)
      {
         yield return WhatDeployingWouldDo(ide, plugin, effect);
         yield break;
      }

      if(effect == Effect.WouldBreakThePluginInTheRunningIde && intoRunningIdes)
      {
         mirror.Mirror(build, pluginFolder);
         yield return $"DEPLOYED {plugin} INTO {ide}, WHICH RUNS: it reloads the build at once, with the registrations it read as it started, and the plugin is broken in it until Magnus restarts it.";
      }
      else
      {
         if(effect is not (Effect.Nothing or Effect.WouldBreakThePluginInTheRunningIde)) mirror.Mirror(build, pluginFolder);
         yield return WhatDeployingDid(ide, plugin, effect);
      }

      if(ide.PluginUpdateSources.NameTheMarketplaceUnlessASourceIsNamed(plugin))
         yield return $"{ide} gets updates of {plugin} from the Marketplace now, so it no longer warns, from its next start, that it can't tell where the plugin came from.";
   }

   /// <summary>What putting <paramref name="build"/> in <paramref name="pluginFolder"/> does in <paramref name="ide"/>, told before doing it.</summary>
   static Effect EffectOfDeploying(EverydayIde ide, string build, string pluginFolder)
   {
      if(FolderMirror.HoldTheSame(build, pluginFolder)) return Effect.Nothing;
      var registeredBefore = PluginRegistrations.InPluginFolder(pluginFolder);
      if(registeredBefore is null) return Effect.LoadedAtTheNextStart;
      if(FolderMirror.WouldAddOrRemoveAFile(build, pluginFolder) || PluginRegistrations.InPluginFolder(build) != registeredBefore)
         return ide.ReloadsChangedPlugins && ide.IsRunning ? Effect.WouldBreakThePluginInTheRunningIde : Effect.TakesARestart;
      return ide.ReloadsChangedPlugins ? Effect.Reload : Effect.LoadedAtTheNextStartAsTheIdeDoesNotReload;
   }

   static string WhatDeployingDid(EverydayIde ide, OurPlugin plugin, Effect effect) => effect switch
   {
      Effect.Nothing => $"{ide} has this build of {plugin} already.",
      Effect.LoadedAtTheNextStart => $"{ide} has {plugin} now, which it loads when Magnus next restarts it.",
      Effect.TakesARestart => $"Deployed {plugin} to {ide}. The build adds or removes a file of the plugin's, or changes what its plugin.xml registers, which a reload doesn't take: {ide} loads it as it next starts.",
      Effect.WouldBreakThePluginInTheRunningIde => $"DIDN'T DEPLOY {plugin} to {ide}: {WhyItWouldBreakThePluginIn(ide)} Close {ide}, and deploy again.",
      Effect.Reload => $"Deployed {plugin} to {ide}, which reloads it, without a restart, when Magnus next switches to its window.",
      Effect.LoadedAtTheNextStartAsTheIdeDoesNotReload => $"Deployed {plugin} to {ide}, which loads it when Magnus next restarts it: it doesn't run with -Didea.auto.reload.plugins=true.",
      _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null)
   };

   static string WhatDeployingWouldDo(EverydayIde ide, OurPlugin plugin, Effect effect) => effect switch
   {
      Effect.Nothing => $"{ide} has this build of {plugin} already.",
      Effect.LoadedAtTheNextStart => $"RESTART: {ide} doesn't have {plugin}, and would load it only when Magnus next restarts it.",
      Effect.TakesARestart => $"RESTART: {plugin} in {ide} would need a restart: the build adds or removes a file of the plugin's, or changes what its plugin.xml registers, which a reload doesn't take.",
      Effect.WouldBreakThePluginInTheRunningIde => $"CLOSE {ide} FIRST: the deploy wouldn't put {plugin} in it while it runs. {WhyItWouldBreakThePluginIn(ide)}",
      Effect.Reload => $"{ide} would reload {plugin}, without a restart, when Magnus next switches to its window.",
      Effect.LoadedAtTheNextStartAsTheIdeDoesNotReload => $"RESTART: {ide} would load {plugin} only when Magnus next restarts it: it doesn't run with -Didea.auto.reload.plugins=true.",
      _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null)
   };

   static string WhyItWouldBreakThePluginIn(EverydayIde ide) =>
      $"The build adds or removes a file of the plugin's, or changes what its plugin.xml registers, and {ide} runs, and reloads changed plugins: it would reload the build at once, with the registrations it read as it started, and the plugin would break, as Claude Sessions did on 2026-11-19.";

   /// <summary>What deploying a build of a plugin does in an IDE.</summary>
   enum Effect
   {
      /// <summary>The IDE has the build already.</summary>
      Nothing,

      /// <summary>The IDE reloads the plugin, without a restart, when Magnus next switches to its window.</summary>
      Reload,

      /// <summary>The IDE doesn't have the plugin, and loads it when it next starts.</summary>
      LoadedAtTheNextStart,

      /// <summary>
      ///   The build adds or removes a file, or registers something else, and the IDE doesn't run, or doesn't reload changed
      ///   plugins: it takes the build as it next starts.
      /// </summary>
      TakesARestart,

      /// <summary>
      ///   The build adds or removes a file, or registers something else, and the IDE runs, and reloads changed plugins:
      ///   it would reload the build at once, its new classes with the registrations it read as it started, which breaks
      ///   the plugin. So it isn't deployed: the IDE must be closed first.
      /// </summary>
      /// <remarks>
      ///   On 2026-11-19 a build of Claude Sessions whose action had moved to another package was deployed so. Rider and
      ///   IntelliJ IDEA reloaded it within seconds, the action's old class wasn't found, and the tool window broke as it
      ///   opened, with Magnus's sessions parked in the Terminal tool window.
      /// </remarks>
      WouldBreakThePluginInTheRunningIde,

      /// <summary>The IDE doesn't reload changed plugins, and loads the build when it next starts.</summary>
      LoadedAtTheNextStartAsTheIdeDoesNotReload
   }
}
