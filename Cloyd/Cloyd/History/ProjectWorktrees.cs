using Cloyd.Projects;
using Cloyd.Projects.Git;

namespace Cloyd.History;

/// <summary>
/// The folders the <see cref="SessionHistoryScope"/>s cover for a project: the git worktree the project is in, and
/// every worktree of its repository, as git lists them. A project that isn't in git has only its own folder, and no
/// <see cref="SessionHistoryScope.AllWorktrees"/>.
/// </summary>
sealed class ProjectWorktrees
{
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
   public IReadOnlyList<SessionHistoryScope> Scopes =>
      [.. SessionHistoryScope.All.Where(it => it != SessionHistoryScope.AllWorktrees || AllWorktrees is not null)];

   /// <summary>
   /// Whether <paramref name="scope"/> covers <paramref name="folder"/>: whether it is one of the scope's folders, or in
   /// one. A folder that isn't known, null, only <see cref="SessionHistoryScope.AllProjects"/> covers.
   /// </summary>
   public bool Cover(SessionHistoryScope scope, string? folder)
   {
      if(scope == SessionHistoryScope.AllProjects) return true;
      if(folder is null) return false;
      return scope == SessionHistoryScope.ThisWorktree ? IsInOrBelow(ThisWorktree) : AllWorktrees!.Any(IsInOrBelow);

      bool IsInOrBelow(string worktree)
      {
         var asked = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
         return asked.Equals(worktree, StringComparison.OrdinalIgnoreCase) || asked.StartsWith(worktree + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
      }
   }

   /// <summary>The worktrees of <paramref name="project"/>, as git tells them.</summary>
   public static async Task<ProjectWorktrees> Of(Project project, CancellationToken cancellation)
   {
      var repository = await GitWorktrees.Read(project, cancellation);
      return repository is null ? new ProjectWorktrees(project.Folder, null)
         : new ProjectWorktrees(repository.Containing(project)?.Root.Folder ?? project.Folder, [.. repository.Checkouts.Select(it => it.Root.Folder)]);
   }
}
