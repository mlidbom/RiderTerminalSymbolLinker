package dev.magnus.claudesessions

import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTab
import com.intellij.ui.content.Content
import kotlinx.coroutines.ExperimentalCoroutinesApi

/**
 * A tab of the [ClaudeSessionsView]: a terminal that runs Claude Code itself, not a shell, so that the tab is the
 * session. It closes when Claude Code exits.
 *
 * The terminal is the reworked terminal's: the terminal plugin creates and runs it, and this plugin only hosts it in
 * its own tool window.
 */
class ClaudeSessionTab(
    val terminal: TerminalToolWindowTab,
    resuming: ClaudeSession?,
) {
    /** The tab's place in the tool window's content manager. */
    val content: Content get() = terminal.content

    /**
     * The session that ran in the tab when it was last looked at: the one it was opened to resume until Claude Code has
     * recorded the session it runs.
     */
    @Volatile
    var session: ClaudeSession? = resuming
        private set

    /** The process the tab's terminal runs, once it has started: Claude Code, or the script that starts it. */
    val process: ProcessHandle? get() = processOf(terminal)

    /**
     * Looks at which session Claude Code runs in the tab now, and returns it: [session], updated. A session changes
     * within the tab when the user starts a new one with `/clear`, or switches to another with `/resume`.
     */
    fun lookAtTheSession(): ClaudeSession? {
        process?.let(ClaudeSession::runningIn)?.let { session = it }
        return session
    }

    companion object {
        /** The process [terminal] runs, once it has started. */
        @OptIn(ExperimentalCoroutinesApi::class)
        fun processOf(terminal: TerminalToolWindowTab): ProcessHandle? {
            val startup = terminal.view.startupOptionsDeferred
            if (!startup.isCompleted || startup.getCompletionExceptionOrNull() != null) return null
            val processId = startup.getCompleted().pid ?: return null
            return ProcessHandle.of(processId).orElse(null)
        }
    }
}
