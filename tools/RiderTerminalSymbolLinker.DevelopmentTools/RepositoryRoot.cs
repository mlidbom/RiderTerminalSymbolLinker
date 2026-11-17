namespace RiderTerminalSymbolLinker.DevelopmentTools;

/// <summary>
///   The root of the repository these tools were built from: the folder above their build output that holds the
///   plugin's Gradle build.
/// </summary>
static class RepositoryRoot
{
   public static string Folder { get; } = FindAbove(AppContext.BaseDirectory);

   /// <summary>
   ///   The name IDEA gives the project, from <c>.idea/.name</c>, which starts the title of the IDEA window that has
   ///   the repository open.
   /// </summary>
   public static string IdeaProjectName => File.ReadAllText(Path.Combine(Folder, ".idea", ".name")).Trim();

   /// <summary>The C# solution the sandbox opens unless it's given another, made for what the plugin links.</summary>
   public static string FixtureSolutionFolder => Path.Combine(Folder, "tests", "FixtureSolution");

   static string FindAbove(string folder) =>
      File.Exists(Path.Combine(folder, "settings.gradle.kts")) && Directory.Exists(Path.Combine(folder, "tools"))
         ? folder
         : FindAbove(Directory.GetParent(folder)?.FullName
                  ?? throw new InvalidOperationException($"No repository root above {AppContext.BaseDirectory}: the tools run from their build output inside the repository."));
}
