namespace Cloyd.History;

/// <summary>
/// Which sessions of the <see cref="ClaudeSessionHistory"/> the <see cref="ClaudeSessionHistoryPicker"/> lists, by the
/// folder each last ran in (<see cref="ProjectWorktrees.Cover"/>). Each covers those before it.
/// </summary>
sealed class ClaudeSessionHistoryScope
{
   ClaudeSessionHistoryScope(string label) => Label = label;

   /// <summary>The sessions that ran in the project's git worktree, or in its folder when it isn't in git: the picker's first.</summary>
   public static readonly ClaudeSessionHistoryScope ThisWorktree = new("This worktree");

   /// <summary>The sessions that ran in any worktree of the project's git repository.</summary>
   public static readonly ClaudeSessionHistoryScope AllWorktrees = new("All worktrees");

   /// <summary>Every session, of every project.</summary>
   public static readonly ClaudeSessionHistoryScope AllProjects = new("All projects");

   /// <summary>Every scope, the narrowest first.</summary>
   public static IReadOnlyList<ClaudeSessionHistoryScope> All { get; } = [ThisWorktree, AllWorktrees, AllProjects];

   /// <summary>What the picker calls it.</summary>
   public string Label { get; }

   public override string ToString() => Label;
}
