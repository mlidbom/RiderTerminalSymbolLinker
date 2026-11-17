using System.Text;
using System.Xml;
using System.Xml.Linq;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   An IDE's record of where each installed plugin gets its updates from: <c>options\pluginUpdateSources.xml</c> in its
///   settings folder. A plugin installed from disk has no entry in it, and the IDE warns as it starts that it can't
///   tell where the plugin came from, as it did for Claude Sessions in Magnus's IntelliJ IDEA (2026-11-17).
/// </summary>
/// <remarks>
///   The IDE's own <c>PluginUpdateSourceServiceImpl</c> keeps it. An entry written while the IDE runs survived its exit
///   in the sandbox Rider (2026-11-17): the IDE writes the file only when it changes a source itself.
/// </remarks>
public sealed class PluginUpdateSources(string settingsFolder)
{
   /// <summary>The source the IDE records for a plugin installed from the JetBrains Marketplace.</summary>
   const string Marketplace = "https://plugins.jetbrains.com/pluginManager";

   const string LineFeed = "\x0A";

   readonly string _file = Path.Combine(settingsFolder, "options", "pluginUpdateSources.xml");

   /// <summary>
   ///   Records the JetBrains Marketplace as where <paramref name="plugin"/> gets its updates from, unless a source is
   ///   recorded for it already. Returns whether it did. It's the source the IDE records when the plugin is installed from
   ///   the Marketplace, and finds no update there for a plugin that isn't published.
   /// </summary>
   public bool NameTheMarketplaceUnlessASourceIsNamed(OurPlugin plugin)
   {
      var record = File.Exists(_file) ? XDocument.Load(_file) : new XDocument(new XElement("application"));
      var sources = SourcesIn(record);
      if(sources.Elements("entry").Any(it => (string?)it.Attribute("pluginId") == plugin.Id)) return false;

      sources.AddFirst(new XElement("entry",
                                    new XAttribute("pluginId", plugin.Id),
                                    new XElement("updateSource", new XAttribute("host", Marketplace))));
      Save(record);
      return true;
   }

   XElement SourcesIn(XDocument record)
   {
      var application = record.Root ?? throw new InvalidOperationException($"{_file} has no root element.");
      var component = application.Elements("component").FirstOrDefault(it => (string?)it.Attribute("name") == "PluginUpdateSources");
      if(component is null) application.Add(component = new XElement("component", new XAttribute("name", "PluginUpdateSources")));
      var sources = component.Element("sources");
      if(sources is null) component.Add(sources = new XElement("sources"));
      return sources;
   }

   /// <summary>
   ///   Saves it as the IDE writes it: UTF-8 without a byte order mark, no XML declaration, lines that end in a line feed
   ///   alone, indented by two spaces.
   /// </summary>
   void Save(XDocument record)
   {
      Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
      using var writer = XmlWriter.Create(_file, new XmlWriterSettings
                                                 {
                                                    Encoding = new UTF8Encoding(false),
                                                    OmitXmlDeclaration = true,
                                                    NewLineChars = LineFeed,
                                                    Indent = true,
                                                    IndentChars = "  "
                                                 });
      record.Save(writer);
   }
}
