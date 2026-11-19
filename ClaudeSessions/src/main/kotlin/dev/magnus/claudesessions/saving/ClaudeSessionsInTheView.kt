package dev.magnus.claudesessions.saving

import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.claudecode.ClaudeSessionId

/**
 * What the [ClaudeSessionKeeper] saves of the [ClaudeSessionsView], for the view to come back as it was: its tabs, in
 * their order, each with its session and its session host, and the session whose tab is selected.
 *
 * @property tabs Each tab that has a session or a session host, in the order of the tabs.
 * @property selected The session of the selected tab, if it has one.
 */
data class ClaudeSessionsInTheView(val tabs: List<SavedTab>, val selected: ClaudeSessionId?) {
    companion object {
        /** No tabs: what the view saves with none, and what's saved before it ever saved any. */
        val NONE = ClaudeSessionsInTheView(emptyList(), null)

        /** Looks at which session each of [tabs] runs, and returns them, with their hosts, and the session of the [selected] tab. */
        fun of(tabs: List<ClaudeSessionTab>, selected: ClaudeSessionTab?): ClaudeSessionsInTheView =
            ClaudeSessionsInTheView(
                tabs.mapNotNull { tab -> tab.lookAtTheSession().let { session -> if (session == null && tab.sessionHost == null) null else SavedTab(session, tab.sessionHost?.id) } },
                selected?.session?.id,
            )
    }
}
