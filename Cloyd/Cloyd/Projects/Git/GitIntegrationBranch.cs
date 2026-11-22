namespace Cloyd.Projects.Git;

/// <summary>The local branch into which work is integrated. Prefer main, then the local counterpart of
/// origin's default branch, then master. A missing target leaves the merge state unknown.</summary>
sealed record GitIntegrationBranch(string Name, string Commit, bool TracksRemote)
{
   public static async Task<GitIntegrationBranch?> Of(Project repository, CancellationToken cancellation)
   {
      var refs = (await GitRead.In(repository, cancellation, "for-each-ref", "--format=%(refname)%00%(objectname)%00%(upstream:remotename)%00%(symref)",
                                   "refs/heads", "refs/remotes/origin/HEAD")).CheckedOutput()
         .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(it => it.TrimEnd('\r').Split('\0')).ToDictionary(it => it[0], StringComparer.Ordinal);
      var defaultRemote = refs.GetValueOrDefault("refs/remotes/origin/HEAD")?[3];
      var localDefault = defaultRemote?.StartsWith("refs/remotes/origin/", StringComparison.Ordinal) == true ? defaultRemote["refs/remotes/origin/".Length..] : null;
      foreach(var name in new[] { "main", localDefault, "master" }.OfType<string>())
         if(refs.TryGetValue($"refs/heads/{name}", out var branch))
            return new GitIntegrationBranch(name, branch[1], branch[2] is not ("" or "."));
      return null;
   }
}
