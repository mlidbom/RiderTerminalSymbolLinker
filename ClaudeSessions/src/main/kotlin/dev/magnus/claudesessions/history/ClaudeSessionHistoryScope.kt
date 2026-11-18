package dev.magnus.claudesessions.history

/**
 * Which sessions of the [ClaudeSessionHistory] the [ClaudeSessionHistoryPicker] lists, by the folder each last ran in.
 * Each covers those before it.
 *
 * @property label What the picker calls it.
 */
enum class ClaudeSessionHistoryScope(val label: String) {
    /** The sessions that ran in the project's worktree, or in its folder when it isn't in git: the picker's first. */
    THIS_WORKTREE("This worktree"),

    /** The sessions that ran in any worktree of the project's git repository. */
    ALL_WORKTREES("All worktrees"),

    /** Every session, of every project. */
    ALL_PROJECTS("All projects"),
}
