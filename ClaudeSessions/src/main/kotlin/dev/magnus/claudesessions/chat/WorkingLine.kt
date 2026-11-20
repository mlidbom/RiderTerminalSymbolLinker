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
import dev.magnus.claudesessions.claudecode.TurnUnderWay
import java.time.Duration
import java.time.Instant
import java.util.Locale
import javax.swing.JComponent
import javax.swing.SwingConstants
import javax.swing.Timer

/**
 * The line over the chat's input box that shows, while Claude works on a turn, that it does, and at what: a spinner,
 * what the [TurnUnderWay] does, such as thinking, writing or the tool call under way, how long the turn has taken so
 * far, its output tokens, and how many commands it has run, as Claude Code's own UI, its desktop app and claude.ai show
 * it. It takes no room while Claude isn't busy.
 *
 * Magnus, 2026-11-20: "The lack of any information about that in the new UI is what keeps me switching back to the TUI
 * constantly at the moment, to see what is going on."
 *
 * How long the turn has taken is counted from its start, or, while no turn under way is told of, as in a session that
 * doesn't load the `tooling-commands` mod, from when the line saw Claude Code turn busy.
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

    /** The turn under way, as Claude Code's mod tells of it, or null while none is told of. */
    private var turn: TurnUnderWay? = null

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

    fun show(turn: TurnUnderWay?) {
        this.turn = turn
        update()
    }

    private fun update() {
        val busySince = busySince
        label.isVisible = busySince != null
        if (busySince == null) return
        val since = turn?.startedAt ?: busySince
        val tokens = turn?.outputTokensSoFar?.takeIf { it > 0 }?.let { "↓ ${inThousands(it)} tokens" }
        val commands = when (commandsOfTheTurn) {
            0 -> null
            1 -> "1 command"
            else -> "$commandsOfTheTurn commands"
        }
        label.text = listOfNotNull(doing(), howLong(Duration.between(since, Instant.now())), tokens, commands).joinToString(" · ")
    }

    /** What the turn does now, in a few words. */
    private fun doing(): String {
        val running = toolCallUnderWay?.let { "Running ${ToolCallHeader.of(it)}" }
        val turn = turn ?: return running ?: "Working…"
        return when (turn.activity) {
            TurnUnderWay.Activity.REQUESTING -> "Waiting for the model…"
            TurnUnderWay.Activity.THINKING -> "Thinking…"
            TurnUnderWay.Activity.WRITING -> "Writing…"
            TurnUnderWay.Activity.WRITING_A_TOOL_CALL -> turn.toolBeingWritten?.let { "Writing the $it call…" } ?: "Writing a tool call…"
            TurnUnderWay.Activity.RUNNING_TOOLS -> running ?: "Running tools…"
            TurnUnderWay.Activity.OTHER -> "Working…"
        }
    }

    /** [tokens], as Claude Code's spinner counts them: `228`, `1.2k`. */
    private fun inThousands(tokens: Long): String = if (tokens < 1000) "$tokens" else "%.1fk".format(Locale.ROOT, tokens / 1000.0)

    /** [taken], as Claude Code's own UI says how long a turn has taken: `42s`, `3m 05s`. */
    private fun howLong(taken: Duration): String =
        if (taken.toMinutes() == 0L) "${taken.toSecondsPart()}s" else "${taken.toMinutes()}m ${"%02d".format(taken.toSecondsPart())}s"

    override fun dispose() {
        ticking.stop()
    }
}
