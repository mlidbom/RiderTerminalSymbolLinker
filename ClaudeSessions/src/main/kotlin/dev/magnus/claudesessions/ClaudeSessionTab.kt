package dev.magnus.claudesessions

import com.intellij.notification.NotificationType
import com.intellij.openapi.Disposable
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
import dev.magnus.claudesessions.hub.ClaudeToolingHub
import dev.magnus.claudesessions.hub.TypingAnswer
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch

/**
 * A tab of the [ClaudeSessionsView], in [project]'s view: a terminal whose shell runs Claude Code, whose command the
 * [ClaudeToolingHub] types into the shell as the tab opens. When Claude Code exits, the shell is left at its prompt, and
 * the tab keeps the session that ran in it last: it closes when the shell exits.
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
     * Takes the tab's chat away, if it has one, giving its content its terminal back, as before: as the tab closes, and
     * before it's parked while the plugin reloads, as the reloaded plugin can't use this build's chat. On the EDT.
     */
    fun takeAwayItsChat() {
        cards?.let(Disposer::dispose)
        cards = null
    }

    /** The process the tab's terminal runs, once it has started: the shell. */
    val process: ProcessHandle? get() = processOf(terminal)

    /** The Claude Code that runs in the tab now, below its shell, as the [ClaudeToolingHub] last told, or null while none does. */
    val claudeCode: RunningClaudeCode? get() = process?.let(ClaudeToolingHub.getInstance().runningClaudeCodes.value::inOrBelow)

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
     * Has the tab's shell run [command], once the shell has started: the [ClaudeToolingHub] types it, quoted as that
     * shell reads it. When it doesn't, the user is told why, and the shell waits at its prompt.
     */
    fun run(command: ClaudeCodeCommand) {
        terminal.view.coroutineScope.launch {
            val shellProcessId = checkNotNull(terminal.view.startupOptionsDeferred.await().pid) { "The terminal started the tab's shell, and doesn't say its process." }
            val shell = ProcessHandle.of(shellProcessId).orElse(null) ?: return@launch // The shell exited at once, and the tab closed with it.
            val answer = ClaudeToolingHub.getInstance().typing.startClaudeCode(shell, command)
            if (answer is TypingAnswer.NotTyped) tellClaudeCodeDidNotStart(answer)
        }
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
