using System.Globalization;

namespace Cloyd.Projects.Git;

/// <summary>The branch headers and changed paths from one porcelain-v2 <c>git status</c> reading.
/// Git reports a path with both staged and unstaged changes once. Request every untracked file
/// so a directory with several new files contributes each of them.</summary>
sealed class GitStatusReport
{
   readonly string[] _lines;
   GitStatusReport(string output) => _lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

   public string? Commit => Header("branch.oid");
   public string? Branch => Header("branch.head");
   public string? Upstream => Header("branch.upstream");
   public int ChangedPaths => _lines.Count(it => it[0] != '#');

   /// <summary>Git omits this header when the configured upstream ref is absent locally.</summary>
   public (int Ahead, int Behind)? AheadAndBehind => Header("branch.ab")?.Split(' ') is [var ahead, var behind]
      ? (int.Parse(ahead, CultureInfo.InvariantCulture), -int.Parse(behind, CultureInfo.InvariantCulture))
      : null;

   public static async Task<GitStatusReport> Read(Project folder, CancellationToken cancellation) =>
      new((await GitRead.In(folder, cancellation, "status", "--porcelain=v2", "--branch", "--ahead-behind", "--untracked-files=all")).CheckedOutput());

   string? Header(string name)
   {
      var prefix = $"# {name} ";
      return _lines.FirstOrDefault(it => it.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
   }
}
