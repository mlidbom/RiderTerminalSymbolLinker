using System.Diagnostics;
using Cloyd.Projects;

namespace Cloyd.History;

/// <summary>
/// The folders the <see cref="ClaudeSessionHistoryScope"/>s cover for a project: the git worktree the project is in, and
/// every worktree of its repository, as git lists them. A project that isn't in git has only its own folder, and no
/// <see cref="ClaudeSessionHistoryScope.AllWorktrees"/>.
/// </summary>
sealed class ProjectWorktrees
{
   /// <summary>What git says, in English, as it fails in a folder that no repository holds.</summary>
   const string WhatGitSaysOutsideARepository = "not a git repository";

   ProjectWorktrees(string thisWorktree, IReadOnlyList<string>? allWorktrees)
   {
      ThisWorktree = thisWorktree;
      AllWorktrees = allWorktrees;
   }

   /// <summary>The project's worktree, by its full path, or its folder when it isn't in git.</summary>
   public string ThisWorktree { get; }

   /// <summary>Every worktree of the project's repository, by their full paths: null when it isn't in git.</summary>
   public IReadOnlyList<string>? AllWorktrees { get; }

   /// <summary>The scopes the picker offers the project: all worktrees only for a project in git.</summary>
   public IReadOnlyList<ClaudeSessionHistoryScope> Scopes =>
      [.. ClaudeSessionHistoryScope.All.Where(it => it != ClaudeSessionHistoryScope.AllWorktrees || AllWorktrees is not null)];

   /// <summary>
   /// Whether <paramref name="scope"/> covers <paramref name="folder"/>: whether it is one of the scope's folders, or in
   /// one. A folder that isn't known, null, only <see cref="ClaudeSessionHistoryScope.AllProjects"/> covers.
   /// </summary>
   public bool Cover(ClaudeSessionHistoryScope scope, string? folder)
   {
      if(scope == ClaudeSessionHistoryScope.AllProjects) return true;
      if(folder is null) return false;
      return scope == ClaudeSessionHistoryScope.ThisWorktree ? IsInOrBelow(ThisWorktree) : AllWorktrees!.Any(IsInOrBelow);

      bool IsInOrBelow(string worktree)
      {
         var asked = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
         return asked.Equals(worktree, StringComparison.OrdinalIgnoreCase) || asked.StartsWith(worktree + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
      }
   }

   /// <summary>The worktrees of <paramref name="project"/>, as git tells them.</summary>
   public static async Task<ProjectWorktrees> Of(Project project, CancellationToken cancellation)
   {
      if(await Git(project, cancellation, "rev-parse", "--show-toplevel") is not [var worktree]) return new ProjectWorktrees(project.Folder, null);

      var worktrees = (await Git(project, cancellation, "worktree", "list", "--porcelain"))!
                     .Where(it => it.StartsWith("worktree ", StringComparison.Ordinal))
                     .Select(it => FullPathOf(it["worktree ".Length..]));
      return new ProjectWorktrees(FullPathOf(worktree), [.. worktrees]);
   }

   /// <summary><paramref name="path"/> as git prints it, with <c>/</c>, as a full path of Windows', with no separator at its end.</summary>
   static string FullPathOf(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

   /// <summary>What git prints, run with <paramref name="arguments"/> in <paramref name="project"/>'s folder, a line each: null outside a repository.</summary>
   static async Task<IReadOnlyList<string>?> Git(Project project, CancellationToken cancellation, params string[] arguments)
   {
      using var git = Process.Start(new ProcessStartInfo("git", arguments)
      {
         WorkingDirectory = project.Folder,
         RedirectStandardOutput = true,
         RedirectStandardError = true,
         CreateNoWindow = true
      })!;
      var printed = git.StandardOutput.ReadToEndAsync(cancellation);
      var complaint = git.StandardError.ReadToEndAsync(cancellation);
      await Task.WhenAll(printed, complaint, git.WaitForExitAsync(cancellation));
      if(git.ExitCode == 0) return printed.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      if(complaint.Result.Contains(WhatGitSaysOutsideARepository)) return null;
      throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed in {project}, exiting with {git.ExitCode}: {complaint.Result}");
   }
}
