using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The few settings the <see cref="SandboxRider"/> needs so that nothing waits for a human: it exits without asking
///   for confirmation, it ends the processes of terminal tabs as it exits without asking whether to, and it trusts the
///   solutions it's given without the "Trust and open?" dialog. And one so that its terminal tabs show what they show in
///   Magnus's Riders: no banner of the MCP Server plugin's above a tab in which Claude Code starts.
/// </summary>
/// <remarks>
///   Rider writes its settings files when it exits, over whatever was written meanwhile, so these are written only
///   while the sandbox isn't running.
/// </remarks>
sealed class SandboxRiderSettings
{
   readonly string _optionsFolder;

   public SandboxRiderSettings(string configurationFolder) => _optionsFolder = Path.Combine(configurationFolder, "options");

   public void Write(string trustedSolutionsFolder)
   {
      Directory.CreateDirectory(_optionsFolder);
      Update("ide.general.xml", "GeneralSettings", component =>
      {
         SetOption(component, "confirmExit", "false");
         // Claude Sessions' tabs run Claude Code, which the terminal would otherwise ask about as the sandbox exits.
         SetOption(component, "processCloseConfirmation", "TERMINATE");
         // A solution handed to the running sandbox opens in a window of its own, as Rider would otherwise ask whether to.
         SetOption(component, "confirmOpenNewProject2", "0");
      });
      Update("trusted-paths.xml", "Trusted.Paths.Settings", component => TrustEverythingUnder(component, trustedSolutionsFolder));
      Update("other.xml", "PropertyService", component => SetProperty(component, TheMcpServersTerminalBannerDismissed, "true"));
   }

   /// <summary>
   ///   The property that the MCP Server plugin's "Do not show again" sets on its banner above a terminal tab in which
   ///   Claude Code starts, which asks to enable the IDE's MCP server: Magnus's Riders, which have the server enabled,
   ///   don't show it.
   /// </summary>
   /// <remarks>
   ///   The banner takes three rows of the tab, and the terminal of Rider 2026.3 doesn't resize the shell for it: Claude
   ///   Code goes on drawing itself three rows taller than the tab shows, and a specification of the size it draws at
   ///   fails for the terminal's bug. intellij-community's terminal code has been changed since to resize the shell for
   ///   it.
   /// </remarks>
   const string TheMcpServersTerminalBannerDismissed = "mcp.server.terminal.promotion.dismissed";

   void Update(string fileName, string componentName, Action<XElement> change)
   {
      var file = Path.Combine(_optionsFolder, fileName);
      var settings = File.Exists(file) ? XDocument.Load(file) : new XDocument(new XElement("application"));
      var application = settings.Root ?? throw new InvalidOperationException($"{file} has no root element.");
      var component = application.Elements("component").FirstOrDefault(it => (string?)it.Attribute("name") == componentName);
      if(component is null) application.Add(component = new XElement("component", new XAttribute("name", componentName)));
      change(component);
      settings.Save(file);
   }

   static void SetOption(XElement component, string name, string value)
   {
      component.Elements("option").Where(it => (string?)it.Attribute("name") == name).Remove();
      component.Add(new XElement("option", new XAttribute("name", name), new XAttribute("value", value)));
   }

   /// <summary>
   ///   Sets an application-wide property in <paramref name="component"/>, the platform's <c>PropertyService</c>, which
   ///   keeps them as a JSON text.
   /// </summary>
   static void SetProperty(XElement component, string name, string value)
   {
      var properties = string.IsNullOrWhiteSpace(component.Value) ? new JsonObject() : JsonNode.Parse(component.Value)!.AsObject();
      if(properties["keyToString"] is not JsonObject keyToString) properties["keyToString"] = keyToString = new JsonObject();
      keyToString[name] = value;
      component.ReplaceNodes(new XCData(properties.ToJsonString(new JsonSerializerOptions { WriteIndented = true })));
   }

   static void TrustEverythingUnder(XElement component, string folder)
   {
      component.Elements("option").Where(it => (string?)it.Attribute("name") == "TRUSTED_PATHS").Remove();
      component.Add(new XElement("option",
                                 new XAttribute("name", "TRUSTED_PATHS"),
                                 new XElement("list", new XElement("option", new XAttribute("value", folder)))));
   }
}
