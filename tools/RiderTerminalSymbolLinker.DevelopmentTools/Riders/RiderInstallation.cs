using System.Text.Json;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>The Rider installed on this machine, which the <see cref="SandboxRider"/> runs and the plugin builds against.</summary>
sealed class RiderInstallation
{
   readonly string _installationFolder;

   RiderInstallation(string installationFolder) => _installationFolder = installationFolder;

   /// <summary>The Rider that JetBrains Toolbox and Rider's own installer put in the user's programs folder.</summary>
   public static RiderInstallation OfThisUser() =>
      new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Rider"));

   /// <summary>The launcher that starts Rider's frontend, which in turn starts its .NET backend.</summary>
   public string Launcher => Path.Combine(_installationFolder, "bin", "rider64.exe");

   /// <summary>The version, such as <c>2026.3</c>, that the Gradle build names its sandbox folder after.</summary>
   public string Version
   {
      get
      {
         using var productInformation = JsonDocument.Parse(File.ReadAllText(Path.Combine(_installationFolder, "product-info.json")));
         return productInformation.RootElement.GetProperty("version").GetString()
             ?? throw new InvalidOperationException($"{_installationFolder}\\product-info.json has no version.");
      }
   }
}
