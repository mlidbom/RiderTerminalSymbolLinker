package dev.magnus.claudesessions.history

import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.claudecode.ClaudeCodeTranscript
import dev.magnus.claudesessions.claudecode.ClaudeSession

/**
 * A session of the [ClaudeSessionHistory], as the [ClaudeSessionHistoryPicker] lists it: what its transcript tells of
 * it, and [where] it is now.
 */
class ClaudeSessionHistoryEntry(private val summary: ClaudeCodeTranscript.Summary, val where: WhereTheSessionIs) {
    val session: ClaudeSession get() = summary.session

    /** What the picker calls the session: its name, or else its last prompt, or else its id. */
    val title: String get() = session.name ?: summary.lastPrompt ?: session.id.value

    /** The session's last prompt, which the picker shows after a [title] that is its name. */
    val lastPrompt: String? get() = summary.lastPrompt?.takeIf { session.name != null }

    val gitBranch: String? get() = summary.gitBranch

    val lastActive get() = summary.lastActive

    /**
     * Whether the entry has every word of [filter] in what the picker shows of it, in any case: its name, its last
     * prompt, its folder and its branch. An empty filter has none, so every entry has them.
     */
    fun matches(filter: String): Boolean {
        val shown = listOfNotNull(session.name, summary.lastPrompt, session.workingDirectory.toString(), summary.gitBranch).joinToString("\n")
        return filter.split(' ', '\t').filter { it.isNotEmpty() }.all { shown.contains(it, ignoreCase = true) }
    }

    /** Does what choosing the entry in the picker of [view]'s project does, as [where] it is decides. */
    fun choose(view: ClaudeSessionsView) {
        where.choose(session, view)
    }
}
