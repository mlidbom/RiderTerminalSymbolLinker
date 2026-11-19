package dev.magnus.claudesessions.chat

import com.intellij.ui.components.JBLabel
import com.intellij.util.ui.JBUI
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import javax.swing.JComponent

/**
 * The line above the chat's input box that says what Claude Code is doing: whether it's busy, waiting for the user in
 * the terminal, or idle, and how many commands the turn has run.
 *
 * Its methods run on the EDT.
 */
class StatusLine {
    private val label = JBLabel().apply { border = JBUI.Borders.empty(2, 6) }

    val component: JComponent get() = label

    private var status = ClaudeCodeStatus.NOT_RUNNING
    private var commandsOfTheTurn = 0

    /** What the line says. */
    val text: String get() = label.text

    init {
        update()
    }

    fun show(status: ClaudeCodeStatus) {
        this.status = status
        update()
    }

    fun show(conversation: Conversation) {
        commandsOfTheTurn = conversation.toolCallsOfTheLastTurn
        update()
    }

    private fun update() {
        val what = when (status) {
            ClaudeCodeStatus.NOT_RUNNING -> "Claude Code isn't running: the terminal shows its shell"
            ClaudeCodeStatus.IDLE -> "Claude is idle"
            ClaudeCodeStatus.BUSY -> "Claude is busy"
            ClaudeCodeStatus.WAITING -> "Claude is waiting for you in the terminal"
        }
        val commands = when (commandsOfTheTurn) {
            0 -> ""
            1 -> " · 1 command this turn"
            else -> " · $commandsOfTheTurn commands this turn"
        }
        label.text = if (status == ClaudeCodeStatus.NOT_RUNNING) what else what + commands
    }
}
