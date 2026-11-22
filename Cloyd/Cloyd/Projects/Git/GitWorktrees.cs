namespace Cloyd.Projects.Git;

/// <summary>The main and linked worktrees of one Git repository as Git lists them. The first record
/// identifies the repository, including when the main repository is bare and only its linked worktrees have files.</summary>
sealed record GitWorktrees(Project Repository, IReadOnlyList<GitWorktree> Checkouts)
{
   public static async Task<GitWorktrees?> Read(Project folder, CancellationToken cancellation)
   {
      if(!Directory.Exists(folder.Folder)) return null;
      var listing = await GitRead.In(folder, cancellation, "worktree", "list", "--porcelain", "-z");
      if(listing.ExitCode != 0 && listing.Error.Contains("not a git repository", StringComparison.Ordinal)) return null;
      var records = listing.CheckedOutput().Split("\0\0", StringSplitOptions.RemoveEmptyEntries);
      var checkouts = new List<GitWorktree>();
      Project? repository = null;
      foreach(var record in records)
      {
         var fields = record.Split('\0');
         var root = new Project(fields.Single(it => it.StartsWith("worktree ", StringComparison.Ordinal))["worktree ".Length..]);
         repository ??= root;
         if(fields.Contains("bare") || fields.Any(it => it == "prunable" || it.StartsWith("prunable ", StringComparison.Ordinal))) continue;
         var branch = fields.FirstOrDefault(it => it.StartsWith("branch refs/heads/", StringComparison.Ordinal))?["branch refs/heads/".Length..];
         checkouts.Add(new GitWorktree(root, branch, root.Equals(repository)));
      }
      return new GitWorktrees(repository ?? throw new InvalidDataException($"Git listed no worktrees for {folder}."), checkouts);
   }

   /// <summary>Finds the most specific registered worktree containing a folder, including nested worktrees.</summary>
   public GitWorktree? Containing(Project folder) =>
      Checkouts.Where(it => it.Root.Contains(folder)).MaxBy(it => it.Root.Folder.Length);
}
