using RiderTerminalSymbolLinker.DevelopmentTools.Processes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Cloyd;

/// <summary>
///   This working tree's build of Cloyd, the application beside the IDEs that shows the projects' Claude Code sessions,
///   whose command Claude Sessions runs as a project opens: <c>Cloyd/Cloyd</c> published in Release, framework-dependent,
///   as an <see cref="InstalledCloyd"/> holds it in its <c>bin</c>.
/// </summary>
static class CloydBuild
{
   static string Project => Path.Combine(RepositoryRoot.Folder, "Cloyd", "Cloyd", "Cloyd.csproj");

   /// <summary>Where <see cref="Published"/> puts the build: in the project's own <c>bin</c>, which git ignores.</summary>
   static string Folder => Path.Combine(RepositoryRoot.Folder, "Cloyd", "Cloyd", "bin", "Published");

   /// <summary>
   ///   Publishes this working tree's Cloyd, and returns the folder that holds the build. Into an empty folder, as a
   ///   publish leaves the files an earlier one put there; its files keep their build's times, so an unchanged build
   ///   mirrors as unchanged.
   /// </summary>
   public static string Published()
   {
      if(Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
      ExternalCommand.Run("dotnet", ["publish", Project, "--configuration", "Release", "--output", Folder, "--nologo", "--verbosity", "quiet"], RepositoryRoot.Folder);
      return Folder;
   }
}
