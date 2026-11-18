package dev.magnus.claudesessions.history

import com.intellij.openapi.application.ApplicationManager
import dev.magnus.claudesessions.ClaudeSession
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.api.AnotherIde

/**
 * Where a session of the [ClaudeSessionHistory] is now, which decides what choosing it in the
 * [ClaudeSessionHistoryPicker] does. A session that runs is never resumed again: two Claude Codes writing to one
 * session would mix their conversations.
 */
sealed interface WhereTheSessionIs {
    /** Whether the session can be chosen in the picker. */
    val canBeChosen: Boolean get() = true

    /** Does what choosing [session] in the picker of [view]'s project does. */
    fun choose(session: ClaudeSession, view: ClaudeSessionsView)

    /** No Claude Code runs it. Choosing it resumes it in a new tab, in the project's folder, where a new session starts. */
    data object NotRunning : WhereTheSessionIs {
        override fun choose(session: ClaudeSession, view: ClaudeSessionsView) {
            view.resumeInTheProjectsFolder(session)
        }
    }

    /**
     * It ran last in a tab of this IDE, whose Claude Code has exited, leaving the shell at its prompt. Choosing it
     * resumes it in that tab.
     */
    data class ExitedInATab(val view: ClaudeSessionsView, val tab: ClaudeSessionTab) : WhereTheSessionIs {
        override fun choose(session: ClaudeSession, view: ClaudeSessionsView) {
            this.view.resumeIn(tab, session)
        }
    }

    /** It runs in a tab of this IDE, in any of its projects. Choosing it brings the tab forward. */
    data class RunningInATab(val view: ClaudeSessionsView, val tab: ClaudeSessionTab) : WhereTheSessionIs {
        override fun choose(session: ClaudeSession, view: ClaudeSessionsView) {
            this.view.bringForward(tab)
        }
    }

    /**
     * It runs in a tab of [ide], as Claude Code's [process]. Choosing it has that IDE bring the tab forward: off the EDT,
     * as the other IDE answers once its own EDT has shown the tab.
     */
    data class RunningInAnotherIde(val ide: AnotherIde, val process: Long) : WhereTheSessionIs {
        override fun choose(session: ClaudeSession, view: ClaudeSessionsView) {
            ApplicationManager.getApplication().executeOnPooledThread { ide.show(session.id, process) }
        }
    }

    /**
     * It runs outside Claude Sessions' tabs, as Claude Code's [process]: in a plain terminal, or in a tab of an IDE whose
     * Claude Sessions doesn't answer. It can't be chosen.
     */
    data class RunningOutsideClaudeSessions(val process: Long) : WhereTheSessionIs {
        override val canBeChosen: Boolean get() = false

        override fun choose(session: ClaudeSession, view: ClaudeSessionsView) {
            error("A session that runs outside Claude Sessions' tabs can't be chosen: ${session.id.value} runs as process $process.")
        }
    }
}
