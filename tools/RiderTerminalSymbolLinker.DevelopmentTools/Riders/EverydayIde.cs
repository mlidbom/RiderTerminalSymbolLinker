using System.Text.Json;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A JetBrains IDE Magnus works in, as JetBrains Toolbox installs it in the user's programs folder, with the everyday
///   settings and plugins folder that every window of it shares.
/// </summary>
/// <param name="Name">The IDE's name, as Magnus would say it.</param>
/// <param name="InstallationFolderName">Its folder in <c>%LOCALAPPDATA%\Programs</c>.</param>
public sealed record EverydayIde(string Name, string InstallationFolderName)
{
   public static readonly EverydayIde Rider = new("Rider", "Rider");
   public static readonly EverydayIde IntelliJIdea = new("IntelliJ IDEA", "IntelliJ IDEA");
   public static readonly EverydayIde PyCharm = new("PyCharm", "PyCharm");

   string InstallationFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", InstallationFolderName);

   public bool IsInstalled => File.Exists(Path.Combine(InstallationFolder, "product-info.json"));

   /// <summary>The settings folder of the installed version, such as <c>%APPDATA%\JetBrains\Rider2026.3</c>.</summary>
   string SettingsFolder
   {
      get
      {
         using var productInformation = JsonDocument.Parse(File.ReadAllText(Path.Combine(InstallationFolder, "product-info.json")));
         var name = productInformation.RootElement.GetProperty("dataDirectoryName").GetString()
                 ?? throw new InvalidOperationException($"{InstallationFolder}\\product-info.json has no dataDirectoryName.");
         return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JetBrains", name);
      }
   }

   /// <summary>The plugins folder every window of the IDE loads its plugins from.</summary>
   public string PluginsFolder => Path.Combine(SettingsFolder, "plugins");

   /// <summary>
   ///   Whether the IDE runs with <c>-Didea.auto.reload.plugins=true</c> in its VM options, and so reloads a plugin whose
   ///   files changed, without a restart, when it next looks at its plugins folder: when Magnus switches to its window.
   ///   His Rider does since 2026-11-17.
   /// </summary>
   public bool ReloadsChangedPlugins =>
      Directory.GetFiles(SettingsFolder, "*.vmoptions").Any(it => File.ReadAllLines(it).Any(line => line.Trim() == "-Didea.auto.reload.plugins=true"));

   public override string ToString() => Name;
}
