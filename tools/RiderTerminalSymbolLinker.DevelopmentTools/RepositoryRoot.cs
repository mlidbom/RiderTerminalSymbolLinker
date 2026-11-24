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

   /// <summary>
   ///   The name of the fixture solution, its folder's and its <c>.slnx</c>'s, prefixed so that Rider's windows and the
   ///   MCP tell it apart from other projects' fixture solutions.
   /// </summary>
   public const string FixtureSolutionName = "RiderTerminalSymbolLinker.FixtureSolution";

   /// <summary>The C# solution the sandbox opens unless it's given another, made for what the plugin links.</summary>
   public static string FixtureSolutionFolder => Path.Combine(Folder, "tests", FixtureSolutionName);

   /// <summary>
   ///   The repository's main checkout, which holds what git doesn't, such as the JDK under <c>.tooling</c>:
   ///   <see cref="Folder"/> itself, or, when that is a worktree, such as a Codex helper's, the checkout it was made from.
   /// </summary>
   /// <remarks>
   ///   A worktree's <c>.git</c> is a file naming its folder in the main checkout's <c>.git</c>, whose <c>commondir</c>
   ///   names the main checkout's <c>.git</c> in turn (git's <c>gitrepository-layout</c>).
   /// </remarks>
   public static string MainCheckoutFolder
   {
      get
      {
         var git = Path.Combine(Folder, ".git");
         if(Directory.Exists(git)) return Folder;
         var worktreesGitFolder = Path.GetFullPath(Path.Combine(Folder, File.ReadAllText(git).Trim()["gitdir:".Length..].Trim()));
         var mainCheckoutsGitFolder = Path.GetFullPath(Path.Combine(worktreesGitFolder, File.ReadAllText(Path.Combine(worktreesGitFolder, "commondir")).Trim()));
         return Path.GetDirectoryName(mainCheckoutsGitFolder)!;
      }
   }

   static string FindAbove(string folder) =>
      File.Exists(Path.Combine(folder, "settings.gradle.kts")) && Directory.Exists(Path.Combine(folder, "tools"))
         ? folder
         : FindAbove(Directory.GetParent(folder)?.FullName
                  ?? throw new InvalidOperationException($"No repository root above {AppContext.BaseDirectory}: the tools run from their build output inside the repository."));
}
