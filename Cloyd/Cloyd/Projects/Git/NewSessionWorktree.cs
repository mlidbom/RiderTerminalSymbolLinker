namespace Cloyd.Projects.Git;

/// <summary>A linked checkout for a newly named session, under the repository's sibling
/// <c>.worktrees</c> folder. Spaces in the session's name become dashes in the branch name.</summary>
static class NewSessionWorktree
{
   public static async Task<Project> Create(Project repository, string? name, CancellationToken cancellation)
   {
      if(string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.'))
         throw new ArgumentException("Enter a worktree and session name that Windows can use as one folder name.");
      var folder = new Project(Path.Combine(repository.Folder + ".worktrees", name));
      var branch = name.Replace(' ', '-');
      var valid = await GitCommand.In(repository, cancellation, "check-ref-format", "--branch", branch);
      if(valid.ExitCode != 0 || valid.Output.TrimEnd() != branch)
         throw new ArgumentException($"Git cannot use '{branch}' as a branch name.");
      if(Directory.Exists(folder.Folder) || File.Exists(folder.Folder))
         throw new IOException($"There is already a folder or file at {folder}.");

      var integration = await GitIntegrationBranch.Of(repository, cancellation);
      cancellation.ThrowIfCancellationRequested();
      (await GitCommand.In(repository, CancellationToken.None, "worktree", "add", "-b", branch, "--", folder.Folder, integration?.Commit ?? "HEAD")).CheckedOutput();
      return folder;
   }
}
