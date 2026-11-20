namespace Cloyd.Projects;

/// <summary>
/// A project whose Claude Code sessions a <see cref="ProjectWindow"/> shows: a folder, as an IDE opens it, by its full
/// path. Two IDEs that have one folder open have one project, and one window.
/// </summary>
sealed class Project : IEquatable<Project>
{
   public Project(string folder) => Folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

   /// <summary>The project's folder, by its full path, with no separator at its end.</summary>
   public string Folder { get; }

   /// <summary>The name of the project's folder, as its window's title shows it.</summary>
   public string Name => Path.GetFileName(Folder);

   /// <summary>Whether <paramref name="other"/> is the same folder: Windows' paths are compared ignoring case.</summary>
   public bool Equals(Project? other) => other is not null && string.Equals(Folder, other.Folder, StringComparison.OrdinalIgnoreCase);

   public override bool Equals(object? other) => other is Project project && Equals(project);

   public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Folder);

   public override string ToString() => Folder;
}
