package dev.magnus.claudesessions.chat

import com.intellij.ui.components.JBLabel
import com.intellij.util.ui.JBUI
import com.intellij.util.ui.NamedColorUtil
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import java.awt.BorderLayout
import javax.swing.JComponent
import javax.swing.JPanel

/**
 * The line above the chat's input box that says what Claude Code is doing: whether it's busy, waiting for the user in
 * the terminal, or idle, and how many commands the turn has run. Below it, when Claude Code didn't get what the chat
 * last gave it, why, until Claude Code gets the next.
 *
 * Its methods run on the EDT.
 */
class StatusLine {
    private val label = JBLabel().apply { border = JBUI.Borders.empty(2, 6) }
    private val whyNotTypedLabel = JBLabel().apply {
        border = JBUI.Borders.empty(0, 6, 2, 6)
        foreground = NamedColorUtil.getErrorForeground()
        isVisible = false
    }

    val component: JComponent = JPanel(BorderLayout()).apply {
        add(label, BorderLayout.NORTH)
        add(whyNotTypedLabel, BorderLayout.SOUTH)
    }

    private var status = ClaudeCodeStatus.NOT_RUNNING
    private var commandsOfTheTurn = 0

    /** What the line says. */
    val text: String get() = label.text

    /** Why Claude Code didn't get what the chat last gave it, or null when it did. */
    var whyNotTyped: String? = null
        private set

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

    /** Shows [why] Claude Code didn't get what the chat last gave it, or, when null, that it did. */
    fun showWhyNotTyped(why: String?) {
        whyNotTyped = why
        whyNotTypedLabel.text = why
        whyNotTypedLabel.toolTipText = why
        whyNotTypedLabel.isVisible = why != null
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
