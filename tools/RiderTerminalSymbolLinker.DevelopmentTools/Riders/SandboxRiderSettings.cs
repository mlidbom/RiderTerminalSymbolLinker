using System.Xml.Linq;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The few settings the <see cref="SandboxRider"/> needs so that nothing waits for a human: it exits without asking
///   for confirmation, it ends the processes of terminal tabs as it exits without asking whether to, and it trusts the
///   solutions it's given without the "Trust and open?" dialog.
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
      });
      Update("trusted-paths.xml", "Trusted.Paths.Settings", component => TrustEverythingUnder(component, trustedSolutionsFolder));
   }

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

   static void TrustEverythingUnder(XElement component, string folder)
   {
      component.Elements("option").Where(it => (string?)it.Attribute("name") == "TRUSTED_PATHS").Remove();
      component.Add(new XElement("option",
                                 new XAttribute("name", "TRUSTED_PATHS"),
                                 new XElement("list", new XElement("option", new XAttribute("value", folder)))));
   }
}
