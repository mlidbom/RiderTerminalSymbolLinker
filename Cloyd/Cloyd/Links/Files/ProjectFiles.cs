using System.Diagnostics;
using Cloyd.Projects;

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

   /// <summary>
   /// The files of <paramref name="project"/> as git lists them: those it tracks, and the new ones it doesn't ignore, so
   /// that a file Claude Code just wrote is one, and nothing a build wrote is. None when no git repository holds the
   /// project's folder. About 100 ms for a repository of a few thousand files.
   /// </summary>
   public static async Task<ProjectFiles> ListedByGitIn(Project project, CancellationToken cancellation)
   {
      using var git = Process.Start(new ProcessStartInfo("git", ["ls-files", "-z", "--cached", "--others", "--exclude-standard"])
      {
         WorkingDirectory = project.Folder,
         RedirectStandardOutput = true,
         RedirectStandardError = true,
         CreateNoWindow = true
      })!;
      var listing = git.StandardOutput.ReadToEndAsync(cancellation);
      var complaint = git.StandardError.ReadToEndAsync(cancellation);
      await Task.WhenAll(listing, complaint, git.WaitForExitAsync(cancellation));
      if(git.ExitCode == 0) return new ProjectFiles(listing.Result.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(it => Path.GetFullPath(it, project.Folder)));
      if(complaint.Result.Contains(WhatGitSaysOutsideARepository)) return new ProjectFiles([]);
      throw new InvalidOperationException($"git couldn't list the files of {project}, exiting with {git.ExitCode}: {complaint.Result}");
   }

   /// <summary>What git says, in English, as it fails in a folder that no repository holds.</summary>
   const string WhatGitSaysOutsideARepository = "not a git repository";

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
