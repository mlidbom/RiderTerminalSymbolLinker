using System.Text.Json.Serialization;
using Cloyd.Projects;

namespace Cloyd.DotNetAnalysisAndRefactoring;

/// <summary>
/// A solution that Rider has loaded, which the <see cref="DotNetAnalysisAndRefactoringMcp"/> serves: its name, its
/// solution file, and the part of its path that tells it from another solution of the same name, as two worktrees of one
/// repository have.
/// </summary>
sealed record LoadedSolution(string Name, [property: JsonPropertyName("path")] string SolutionFile, string? UniquePathSegment)
{
   /// <summary>
   /// What a call names the solution by, as its <c>solutionName</c>: the <see cref="UniquePathSegment"/>, where the MCP
   /// gives one, as a bare name may reach another solution of that name.
   /// </summary>
   public string RoutingKey => UniquePathSegment ?? Name;

   /// <summary>Whether the solution's file is in <paramref name="project"/>'s folder, or in a folder in it.</summary>
   public bool IsIn(Project project) => Path.GetFullPath(SolutionFile).StartsWith(project.Folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
