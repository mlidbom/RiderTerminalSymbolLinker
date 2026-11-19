package dev.magnus.claudesessions

import com.intellij.openapi.application.EDT
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTab
import com.intellij.ui.content.Content
import dev.magnus.claudesessions.claudecode.ClaudeCodeCommand
import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.claudecode.RunningClaudeCode
import dev.magnus.claudesessions.claudecode.Shell
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch

/**
 * A tab of the [ClaudeSessionsView]: a terminal whose shell runs Claude Code, which the tab types the command for as it
 * opens. When Claude Code exits, the shell is left at its prompt, and the tab keeps the session that ran in it last: it
 * closes when the shell exits.
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
     * The session that ran in the tab when it was last looked at, or runs in it still: the one it was opened to resume
     * until Claude Code has recorded the session it runs.
     */
    @Volatile
    var session: ClaudeSession? = resuming
        private set

    /** The process the tab's terminal runs, once it has started: the shell. */
    val process: ProcessHandle? get() = processOf(terminal)

    /** The Claude Code that runs in the tab now, below its shell, or null while none does. */
    val claudeCode: RunningClaudeCode? get() = process?.let(RunningClaudeCode::inOrBelow)

    /**
     * Looks at which session Claude Code runs in the tab now, and returns it: [session], updated. A session changes
     * within the tab when the user starts a new one with `/clear`, or switches to another with `/resume`. While no Claude
     * Code runs, it's the one that ran last.
     */
    fun lookAtTheSession(): ClaudeSession? {
        claudeCode?.let { session = it.session }
        return session
    }

    /**
     * Has the tab's shell run [command]: types it, as that [Shell] reads it, once the shell has started and it's known
     * which one runs.
     */
    fun run(command: ClaudeCodeCommand) {
        terminal.view.coroutineScope.launch(Dispatchers.EDT) {
            val shell = Shell.runBy(terminal.view.startupOptionsDeferred.await().shellCommand)
            terminal.view.createSendTextBuilder().shouldExecute().send(shell.lineThatRuns(command))
        }
    }

    /** Has the tab's shell, at its prompt since the tab's Claude Code exited, resume [session]. */
    fun resume(session: ClaudeSession) {
        run(ClaudeCodeCommand.resuming(session))
        this.session = session
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
