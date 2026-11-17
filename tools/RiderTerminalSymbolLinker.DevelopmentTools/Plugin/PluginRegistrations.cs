using System.IO.Compression;
using System.Xml.Linq;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

/// <summary>
///   What a build of the plugin registers with Rider: its <c>plugin.xml</c>, the plugin's descriptor, apart from what only
///   the Marketplace and Rider's Plugins dialog show, its version, change notes and description, and apart from comments
///   and the whitespace between elements. That is its dependencies, extensions and actions.
/// </summary>
/// <remarks>
///   A running Rider that reloads the plugin reuses the descriptor it read when it started, and loads only the new
///   classes. So when two builds register different things, the new one needs a restart: a reload would run its classes
///   with the old build's extensions. The build writes the changelog into the change notes, so comparing the whole file
///   would ask for a restart after every changelog edit.
/// </remarks>
sealed record PluginRegistrations(string Xml)
{
   /// <summary>
   ///   What the build of the plugin in <paramref name="pluginFolder"/> registers, or null when the folder holds no build.
   /// </summary>
   public static PluginRegistrations? InPluginFolder(string pluginFolder)
   {
      var lib = Path.Combine(pluginFolder, "lib");
      if(!Directory.Exists(lib)) return null;
      foreach(var jar in Directory.GetFiles(lib, "*.jar"))
      {
         using var archive = ZipFile.OpenRead(jar);
         if(archive.GetEntry("META-INF/plugin.xml") is not {} descriptor) continue;
         using var stream = descriptor.Open();
         return Of(XDocument.Load(stream));
      }

      return null;
   }

   /// <summary>
   ///   What <paramref name="descriptor"/>, loaded without the whitespace between its elements, registers: without its
   ///   comments, and without what only the Marketplace shows.
   /// </summary>
   static PluginRegistrations Of(XDocument descriptor)
   {
      descriptor.DescendantNodes().OfType<XComment>().Remove();
      descriptor.Root!.Elements().Where(it => it.Name.LocalName is "version" or "change-notes" or "description").Remove();
      return new PluginRegistrations(descriptor.ToString(SaveOptions.DisableFormatting));
   }
}
