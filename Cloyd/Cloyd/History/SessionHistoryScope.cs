namespace Cloyd.History;

/// <summary>
/// Which sessions of the <see cref="SessionHistory"/> the <see cref="SessionHistoryPicker"/> lists, by the
/// folder each last ran in (<see cref="ProjectWorktrees.Cover"/>). Each covers those before it.
/// </summary>
sealed class SessionHistoryScope
{
   SessionHistoryScope(string label) => Label = label;

   /// <summary>The sessions that ran in the project's git worktree, or in its folder when it isn't in git: the picker's first.</summary>
   public static readonly SessionHistoryScope ThisWorktree = new("This worktree");

   /// <summary>The sessions that ran in any worktree of the project's git repository.</summary>
   public static readonly SessionHistoryScope AllWorktrees = new("All worktrees");

   /// <summary>Every session, of every project.</summary>
   public static readonly SessionHistoryScope AllProjects = new("All projects");

   /// <summary>Every scope, the narrowest first.</summary>
   public static IReadOnlyList<SessionHistoryScope> All { get; } = [ThisWorktree, AllWorktrees, AllProjects];

   /// <summary>What the picker calls it.</summary>
   public string Label { get; }

   public override string ToString() => Label;
}
