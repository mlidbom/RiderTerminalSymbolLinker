package dev.magnus.claudesessions

import com.intellij.notification.NotificationType
import com.intellij.openapi.Disposable
import com.intellij.openapi.application.EDT
import com.intellij.openapi.diagnostic.thisLogger
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.Disposer
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTab
import com.intellij.ui.content.Content
import dev.magnus.claudesessions.chat.ChatAndTerminalCards
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChatProvider
import dev.magnus.claudesessions.claudecode.ClaudeCodeCommand
import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.claudecode.RunningClaudeCode
import dev.magnus.claudesessions.claudecode.RunningClaudeCodes
import dev.magnus.claudesessions.hub.ClaudeToolingHub
import dev.magnus.claudesessions.hub.TypingAnswer
import dev.magnus.claudesessions.sessionhosts.SessionHost
import kotlin.time.Duration.Companion.milliseconds
import kotlin.time.Duration.Companion.seconds
import kotlin.time.TimeSource
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.delay
import kotlinx.coroutines.future.await
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * A tab of the [ClaudeSessionsView], in [project]'s view: a terminal that shows a shell which runs Claude Code, whose
 * command the [ClaudeToolingHub] types into the shell as the tab opens. When Claude Code exits, the shell is left at its
 * prompt, and the tab keeps the session that ran in it last: it closes when the shell exits.
 *
 * The shell runs in the tab's [sessionHost], outside the IDE, and the tab's terminal runs the host's attach client,
 * which shows it: so the session outlives the IDE. A terminal tab that came into the view's tool window otherwise runs
 * its shell itself, and has no host.
 *
 * The terminal is the reworked terminal's: the terminal plugin creates and runs it, and this plugin only hosts it in
 * its own tool window.
 *
 * A tab that a [ClaudeCodeBehindTheChatProvider] gives a Claude Code behind a chat shows its session as a chat too: its
 * content is then its [cards], the chat and the terminal, one shown at a time. Today only the sandbox IDE's tabs do.
 */
class ClaudeSessionTab(
    private val project: Project,
    val terminal: TerminalToolWindowTab,
    resuming: ClaudeSession?,
    val sessionHost: SessionHost?,
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

    /** The tab's chat and its terminal, of which it shows one at a time, or null while it shows its terminal alone. */
    var cards: ChatAndTerminalCards? = null
        private set

    /**
     * Gives the tab a chat, if a [ClaudeCodeBehindTheChatProvider] has a Claude Code behind one for it, kept until
     * [takeAwayItsChat], or until [parent] is disposed. On the EDT.
     */
    fun giveItAChat(parent: Disposable) {
        if (cards != null) return
        val claudeCode = ClaudeCodeBehindTheChatProvider.behindTheChatOf(this) ?: return
        cards = ChatAndTerminalCards(project, content, claudeCode).also { Disposer.register(parent, it) }
    }

    /**
     * Takes the tab's chat away, if it has one, giving its content its terminal back, as before: as the tab closes. On
     * the EDT.
     */
    fun takeAwayItsChat() {
        cards?.let(Disposer::dispose)
        cards = null
    }

    /**
     * The process the tab's terminal runs, once it has started: its session host's attach client, or the shell, for a tab
     * without a host. A tab's terminal starts its process only once the tab is first shown.
     */
    val process: ProcessHandle? get() = processOf(terminal)

    /**
     * Whether the tab is closing because its attach client ended by itself: its shell exited, which ended its session
     * host, or another attach client took the session over. Its host is then not the tab's to end.
     */
    var closedAsItsAttachClientEnded: Boolean = false
        private set

    /** The Claude Code that runs in the tab now, as the [ClaudeToolingHub] last told, or null while none does. */
    val claudeCode: RunningClaudeCode? get() = claudeCodeAmong(ClaudeToolingHub.getInstance().runningClaudeCodes.value)

    /** The one of [running] that runs in the tab: in its session host's console, or below its shell, for a tab without a host. */
    fun claudeCodeAmong(running: RunningClaudeCodes): RunningClaudeCode? =
        if (sessionHost != null) running.inTheHost(sessionHost.id) else process?.let(running::inOrBelow)

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
     * Has the tab's shell run [command]: the [ClaudeToolingHub] types it, quoted as that shell reads it. When it doesn't,
     * the user is told why, and the shell waits at its prompt.
     */
    fun run(command: ClaudeCodeCommand) {
        terminal.view.coroutineScope.launch {
            val shell = shell() ?: return@launch // The shell exited, and the tab closes with it.
            val answer = ClaudeToolingHub.getInstance().typing.startClaudeCode(shell, command)
            if (answer is TypingAnswer.NotTyped) tellClaudeCodeDidNotStart(answer)
        }
    }

    /**
     * Has the tab's shell, which its session host has just started, run [command], as [run] does. A shell that starts
     * may run programs of its own, as Git Bash's login profile does, and the hub refuses to type while one runs, as it
     * would get the line: so it's asked again until [A_SHELL_MAY_TAKE_TO_START] has passed. Seen on 2026-11-19, with the
     * hub asked 90 ms after the host started Git Bash.
     */
    fun runInItsNewShell(command: ClaudeCodeCommand) {
        terminal.view.coroutineScope.launch {
            val shell = shell() ?: return@launch // The shell exited, and the tab closes with it.
            val started = TimeSource.Monotonic.markNow()
            var answer = ClaudeToolingHub.getInstance().typing.startClaudeCode(shell, command)
            while (answer is TypingAnswer.NotTyped && answer.reason == SHELL_RUNS_A_PROGRAM && started.elapsedNow() < A_SHELL_MAY_TAKE_TO_START) {
                delay(BETWEEN_TRIES)
                answer = ClaudeToolingHub.getInstance().typing.startClaudeCode(shell, command)
            }
            if (answer is TypingAnswer.NotTyped) tellClaudeCodeDidNotStart(answer)
        }
    }

    /**
     * Has [close] close the tab once its attach client ends by itself, marking it [closedAsItsAttachClientEnded]: a tab
     * with a session host has its terminal leave the tab open as its process ends, so that the view can tell this close
     * from the user's, which ends the host. On the EDT.
     */
    fun closeAsItsAttachClientEnds(close: () -> Unit) {
        terminal.view.coroutineScope.launch {
            val attachClient = checkNotNull(terminal.view.startupOptionsDeferred.await().pid) { "The terminal started the tab's attach client, and doesn't say its process." }
            ProcessHandle.of(attachClient).orElse(null)?.onExit()?.await()
            withContext(Dispatchers.EDT) {
                closedAsItsAttachClientEnded = true
                close()
            }
        }
    }

    /** The shell Claude Code is typed into: its session host's, or, for a tab without a host, its terminal's, once it has started. */
    private suspend fun shell(): ProcessHandle? {
        if (sessionHost != null) return sessionHost.shell?.takeIf { it.isAlive }
        val shellProcessId = checkNotNull(terminal.view.startupOptionsDeferred.await().pid) { "The terminal started the tab's shell, and doesn't say its process." }
        return ProcessHandle.of(shellProcessId).orElse(null)
    }

    private fun tellClaudeCodeDidNotStart(answer: TypingAnswer.NotTyped) {
        thisLogger().warn("Claude Code wasn't started in the tab ${content.displayName}: ${answer.why} (${answer.reason})")
        ClaudeSessionsNotifications.tell(project, "Claude Code didn't start in the tab ${content.displayName}", answer.why, NotificationType.WARNING)
    }

    /** Has the tab's shell, at its prompt since the tab's Claude Code exited, resume [session]. */
    fun resume(session: ClaudeSession) {
        run(ClaudeCodeCommand.Resuming(session))
        this.session = session
    }

    companion object {
        /** The hub's reason for not typing into a shell while a program runs in it. */
        private const val SHELL_RUNS_A_PROGRAM = "shellRunsAProgram"

        /** How long a new shell may run programs of its own as it starts, such as its profile's. */
        private val A_SHELL_MAY_TAKE_TO_START = 10.seconds

        private val BETWEEN_TRIES = 250.milliseconds

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
