package dev.magnus.claudesessions.chat

import com.intellij.openapi.Disposable
import com.intellij.openapi.util.Disposer
import com.intellij.ui.AnimatedIcon
import com.intellij.ui.components.JBLabel
import com.intellij.util.ui.JBUI
import com.intellij.util.ui.UIUtil
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallHeader
import java.time.Duration
import java.time.Instant
import javax.swing.JComponent
import javax.swing.SwingConstants
import javax.swing.Timer

/**
 * The line over the chat's input box that shows, while Claude works on a turn, that it does, and at what: a spinner,
 * the tool call under way, if one is, how long the turn has taken so far, and how many commands it has run, as Claude
 * Code's own UI, its desktop app and claude.ai show it. It takes no room while Claude isn't busy.
 *
 * Magnus, 2026-11-20: "The lack of any information about that in the new UI is what keeps me switching back to the TUI
 * constantly at the moment, to see what is going on."
 *
 * How long the turn has taken is counted from when the line saw Claude Code turn busy.
 *
 * Its methods run on the EDT.
 */
class WorkingLine(parent: Disposable) : Disposable {
    private val label = JBLabel(AnimatedIcon.Default.INSTANCE, SwingConstants.LEADING).apply {
        border = JBUI.Borders.empty(0, 6, 4, 6)
        foreground = UIUtil.getContextHelpForeground()
        isVisible = false
    }

    val component: JComponent get() = label

    /** When the line saw Claude Code turn busy, or null while it isn't. */
    private var busySince: Instant? = null

    /** The tool call under way, the last of the turn's that has no result yet, or null while none is. */
    private var toolCallUnderWay: ToolCall? = null

    private var commandsOfTheTurn = 0

    /** Has the line count the seconds while Claude is busy. */
    private val ticking = Timer(1000) { update() }

    /** What the line says, or null while it doesn't show. */
    val text: String? get() = label.text.takeIf { label.isVisible }

    init {
        Disposer.register(parent, this)
    }

    fun show(status: ClaudeCodeStatus) {
        val isBusy = status == ClaudeCodeStatus.BUSY
        if (isBusy && busySince == null) {
            busySince = Instant.now()
            ticking.start()
        }
        if (!isBusy) {
            busySince = null
            ticking.stop()
        }
        update()
    }

    fun show(conversation: Conversation) {
        val turn = conversation.items.takeLastWhile { it !is Prompt && it !is PromptFromElsewhere }
        toolCallUnderWay = turn.filterIsInstance<ToolCall>().lastOrNull { it.result == null }
        commandsOfTheTurn = conversation.toolCallsOfTheLastTurn
        update()
    }

    private fun update() {
        val since = busySince
        label.isVisible = since != null
        if (since == null) return
        val doing = toolCallUnderWay?.let { "Running ${ToolCallHeader.of(it)}" } ?: "Working…"
        val commands = when (commandsOfTheTurn) {
            0 -> null
            1 -> "1 command"
            else -> "$commandsOfTheTurn commands"
        }
        label.text = listOfNotNull(doing, howLong(Duration.between(since, Instant.now())), commands).joinToString(" · ")
    }

    /** [taken], as Claude Code's own UI says how long a turn has taken: `42s`, `3m 05s`. */
    private fun howLong(taken: Duration): String =
        if (taken.toMinutes() == 0L) "${taken.toSecondsPart()}s" else "${taken.toMinutes()}m ${"%02d".format(taken.toSecondsPart())}s"

    override fun dispose() {
        ticking.stop()
    }
}
