namespace Cloyd.Links.Files;

/// <summary>
/// The files of a project, by their full paths: what a path in a terminal's output must name to link
/// (<see cref="FileReferences"/>).
/// </summary>
/// <remarks>
/// A path names a file when its every segment lines up with the end of the file's path, ignoring case and which separator:
/// <c>Shell/AppShell.cs</c> and <c>C:\Dev\Vantage\Shell\AppShell.cs</c> name <c>C:\Dev\Vantage\Shell\AppShell.cs</c>, and
/// <c>nonexisting/Shell/AppShell.cs</c> names nothing. A bare name names every file of that name. A path that ends with a
/// separator names a folder.<br/>
/// The symbol linker's <c>SolutionFileIndex.kt</c>, ported.
/// </remarks>
sealed class ProjectFiles
{
   readonly ILookup<string, string> _byName;

   public ProjectFiles(IEnumerable<string> fullPaths) => _byName = fullPaths.ToLookup(it => SegmentsOf(it)[^1], StringComparer.OrdinalIgnoreCase);

   /// <summary>Whether <paramref name="path"/> names one of the files, or several.</summary>
   public bool HasOneWhosePathEndsWith(string path) => WhosePathEndsWith(path).Count > 0;

   /// <summary>The full paths of the files that <paramref name="path"/> names: every one of a bare name's.</summary>
   public IReadOnlyList<string> WhosePathEndsWith(string path)
   {
      if(path.EndsWith('/') || path.EndsWith('\\')) return [];
      var segments = SegmentsOf(path);
      return segments.Count == 0 ? [] : [.. _byName[segments[^1]].Where(it => EndsWith(SegmentsOf(it), segments))];
   }

   static bool EndsWith(IReadOnlyList<string> pathOfAFile, IReadOnlyList<string> segments) =>
      segments.Count <= pathOfAFile.Count && pathOfAFile.Skip(pathOfAFile.Count - segments.Count).SequenceEqual(segments, StringComparer.OrdinalIgnoreCase);

   /// <summary>The folders and the name in <paramref name="path"/>, leaving out a drive such as <c>C:</c>.</summary>
   static IReadOnlyList<string> SegmentsOf(string path) => [.. path.Split('/', '\\').Where(it => it.Length > 0 && !it.EndsWith(':'))];
}
