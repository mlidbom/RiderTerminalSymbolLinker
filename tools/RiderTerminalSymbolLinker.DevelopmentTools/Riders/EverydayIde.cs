using System.Diagnostics;
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

   public static IReadOnlyList<EverydayIde> All { get; } = [Rider, IntelliJIdea, PyCharm];

   /// <summary>The IDE named <paramref name="name"/>, as Magnus would say it, such as "IntelliJ IDEA", in any case.</summary>
   public static EverydayIde Named(string name) =>
      All.SingleOrDefault(it => string.Equals(it.Name, name, StringComparison.OrdinalIgnoreCase))
   ?? throw new ArgumentException($"No IDE Magnus works in is named '{name}': {string.Join(", ", All)} are.");

   string InstallationFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", InstallationFolderName);

   public bool IsInstalled => File.Exists(Path.Combine(InstallationFolder, "product-info.json"));

   /// <summary>The settings folder of the installed version, such as <c>%APPDATA%\JetBrains\Rider2026.3</c>.</summary>
   string SettingsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JetBrains", DataDirectoryName);

   /// <summary>The system folder of the installed version, its caches and logs, such as <c>%LOCALAPPDATA%\JetBrains\Rider2026.3</c>.</summary>
   string SystemFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains", DataDirectoryName);

   /// <summary>What the installed version names its settings and system folders, such as <c>Rider2026.3</c>.</summary>
   string DataDirectoryName =>
      ProductInformation().GetProperty("dataDirectoryName").GetString()
   ?? throw new InvalidOperationException($"{InstallationFolder}\\product-info.json has no dataDirectoryName.");

   /// <summary>The name of the process the IDE runs as on Windows, its launcher's, such as <c>rider64</c>.</summary>
   string ProcessName =>
      Path.GetFileNameWithoutExtension(ProductInformation().GetProperty("launch").EnumerateArray()
                                                           .First(it => it.GetProperty("os").GetString() == "Windows")
                                                           .GetProperty("launcherPath").GetString()!);

   JsonElement ProductInformation()
   {
      using var productInformation = JsonDocument.Parse(File.ReadAllText(Path.Combine(InstallationFolder, "product-info.json")));
      return productInformation.RootElement.Clone();
   }

   /// <summary>
   ///   Whether the IDE runs now: the process its system folder's <c>.pid</c> names is alive, and is the IDE's. The IDE
   ///   writes the file as it starts, and leaves it behind when it exits, for a later process to get the same id.
   /// </summary>
   public bool IsRunning
   {
      get
      {
         var pidFile = Path.Combine(SystemFolder, ".pid");
         if(!File.Exists(pidFile) || !int.TryParse(File.ReadAllText(pidFile).Trim(), out var processId)) return false;
         try
         {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited && process.ProcessName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase);
         }
         catch(ArgumentException) // No process runs with the id: the IDE that wrote the file has exited.
         {
            return false;
         }
      }
   }

   /// <summary>The plugins folder every window of the IDE loads its plugins from.</summary>
   public string PluginsFolder => Path.Combine(SettingsFolder, "plugins");

   public PluginUpdateSources PluginUpdateSources => new(SettingsFolder);

   /// <summary>
   ///   Whether the IDE runs with <c>-Didea.auto.reload.plugins=true</c> in its VM options, and so reloads a plugin whose
   ///   files changed, without a restart, when it next looks at its plugins folder: when Magnus switches to its window.
   ///   His Rider, IntelliJ IDEA and PyCharm do, since 2026-11-17.
   /// </summary>
   public bool ReloadsChangedPlugins =>
      Directory.GetFiles(SettingsFolder, "*.vmoptions").Any(it => File.ReadAllLines(it).Any(line => line.Trim() == "-Didea.auto.reload.plugins=true"));

   public override string ToString() => Name;
}
