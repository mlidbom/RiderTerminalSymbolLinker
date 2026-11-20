package dev.magnus.claudesessions.chat

import com.intellij.ui.components.JBLabel
import com.intellij.util.ui.JBUI
import com.intellij.util.ui.NamedColorUtil
import com.intellij.util.ui.UIUtil
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.claudecode.ClaudeCodesStatusLine
import java.awt.BorderLayout
import javax.swing.JComponent
import javax.swing.JPanel

/**
 * The line under the chat's input box that says what Claude Code is doing: whether it's busy, waiting for the user in
 * the terminal, or idle; and at its right, what Claude Code last showed under its own input box, its
 * [ClaudeCodesStatusLine]: its model, effort level, permission mode and context used, as the desktop app's footer shows
 * "Opus 5.5 · High · Auto". Over it, when Claude Code didn't get what the chat last gave it, why, until Claude Code gets
 * the next.
 *
 * Its methods run on the EDT.
 */
class StatusLine {
    private val label = JBLabel().apply {
        border = JBUI.Borders.empty(3, 6)
        foreground = UIUtil.getContextHelpForeground()
    }
    private val claudeCodesStatusLineLabel = JBLabel().apply {
        border = JBUI.Borders.empty(3, 6)
        foreground = UIUtil.getContextHelpForeground()
    }
    private val whyNotTypedLabel = JBLabel().apply {
        border = JBUI.Borders.empty(3, 6, 0, 6)
        foreground = NamedColorUtil.getErrorForeground()
        isVisible = false
    }

    /** Why Claude Code didn't get what the chat last gave it, when it didn't, over what Claude Code is doing. */
    val component: JComponent = JPanel(BorderLayout()).apply {
        isOpaque = false
        add(whyNotTypedLabel, BorderLayout.NORTH)
        add(JPanel(BorderLayout()).apply {
            isOpaque = false
            add(label, BorderLayout.CENTER)
            add(claudeCodesStatusLineLabel, BorderLayout.EAST)
        }, BorderLayout.SOUTH)
    }

    private var status = ClaudeCodeStatus.NOT_RUNNING

    /** What the line says of what Claude Code is doing. */
    val text: String get() = label.text

    /** What the line shows of Claude Code's own status line, or null while it shows none. */
    val claudeCodesStatusLine: String? get() = claudeCodesStatusLineLabel.text.takeUnless { it.isNullOrEmpty() }

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

    /** Shows what Claude Code last showed under its own input box: nothing, when null, as while no Claude Code runs. */
    fun show(statusLine: ClaudeCodesStatusLine?) {
        claudeCodesStatusLineLabel.text = statusLine?.text
        claudeCodesStatusLineLabel.toolTipText = statusLine?.let { "What Claude Code shows under its own input box" }
    }

    /** Shows [why] Claude Code didn't get what the chat last gave it, or, when null, that it did. */
    fun showWhyNotTyped(why: String?) {
        whyNotTyped = why
        whyNotTypedLabel.text = why
        whyNotTypedLabel.toolTipText = why
        whyNotTypedLabel.isVisible = why != null
    }

    private fun update() {
        label.text = when (status) {
            ClaudeCodeStatus.NOT_RUNNING -> "Claude Code isn't running: the terminal shows its shell"
            ClaudeCodeStatus.IDLE -> "Claude is idle"
            ClaudeCodeStatus.BUSY -> "Claude is busy"
            ClaudeCodeStatus.WAITING -> "Claude is waiting for you in the terminal"
        }
    }
}
