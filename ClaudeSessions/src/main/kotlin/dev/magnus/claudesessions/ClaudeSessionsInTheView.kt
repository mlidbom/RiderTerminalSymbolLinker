package dev.magnus.claudesessions

/**
 * What the [ClaudeSessionKeeper] saves of the [ClaudeSessionsView], for the view to come back as it was: the sessions its
 * tabs run, in their order, and the one whose tab is selected.
 *
 * @property sessions The session each tab runs, or ran last, in the order of the tabs. A tab whose Claude Code hasn't
 * recorded its session yet has none to save.
 * @property selected The session of the selected tab, if it has one.
 */
data class ClaudeSessionsInTheView(val sessions: List<ClaudeSession>, val selected: ClaudeSessionId?) {
    companion object {
        /** No sessions: what the view saves with no tabs, and what's saved before it ever saved any. */
        val NONE = ClaudeSessionsInTheView(emptyList(), null)

        /** Looks at which session each of [tabs] runs, and returns them, with that of the [selected] tab. */
        fun of(tabs: List<ClaudeSessionTab>, selected: ClaudeSessionTab?): ClaudeSessionsInTheView =
            ClaudeSessionsInTheView(tabs.mapNotNull { it.lookAtTheSession() }, selected?.session?.id)
    }
}
