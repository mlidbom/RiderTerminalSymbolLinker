package dev.magnus.claudesessions.newsession

import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.popup.JBPopup
import com.intellij.openapi.ui.popup.JBPopupFactory
import com.intellij.openapi.ui.popup.JBPopupListener
import com.intellij.openapi.ui.popup.LightweightWindowEvent
import com.intellij.ui.components.JBTextField
import com.intellij.util.ui.JBUI
import dev.magnus.claudesessions.ClaudeSessionsView
import java.awt.BorderLayout
import java.awt.Component
import java.util.concurrent.ConcurrentHashMap
import javax.swing.JPanel

/**
 * The popup that the New Claude Session action opens: a text field for the name of the Claude Code session to start.
 * Enter starts it in a new tab of the project's [ClaudeSessionsView], in the project's folder: named what the field
 * holds, or without a name when the field is empty. Escape, or a click outside, closes the popup and starts none.
 *
 * Its members run on the EDT.
 */
class NewClaudeSessionPopup private constructor(private val project: Project) {
    private val nameField = JBTextField(30).apply { emptyText.text = "Name, or leave empty for none" }

    private val popup: JBPopup

    /** What is typed into its field: the name the session is to have. */
    var name: String
        get() = nameField.text
        set(value) {
            nameField.text = value
        }

    init {
        nameField.addActionListener { start() }
        val panel = JPanel(BorderLayout()).apply {
            border = JBUI.Borders.empty(4)
            add(nameField, BorderLayout.CENTER)
        }
        popup = JBPopupFactory.getInstance().createComponentPopupBuilder(panel, nameField)
            .setTitle("New Claude Session")
            .setProject(project)
            .setRequestFocus(true)
            .setCancelOnClickOutside(true)
            .createPopup()
        popup.addListener(object : JBPopupListener {
            override fun onClosed(event: LightweightWindowEvent) {
                shown.remove(project, this@NewClaudeSessionPopup)
            }
        })
    }

    /** Has the field take Enter, as from the keyboard, which starts the session: for the development observation channel. */
    fun pressEnter() {
        nameField.postActionEvent()
    }

    /** Closes the popup, starting no session, as Escape does. */
    fun close() {
        popup.cancel()
    }

    /** Closes the popup, and starts the session in a new tab: named what the field holds, unless that's empty. */
    private fun start() {
        popup.closeOk(null)
        ClaudeSessionsView.openIn(project).startNewSession(nameField.text.trim().ifEmpty { null })
    }

    /** Shows the popup below [button], or else, when there's none to show it below, in the middle of the project's window. */
    private fun show(button: Component?) {
        shown[project] = this
        if (button != null && button.isShowing) popup.showUnderneathOf(button) else popup.showCenteredInCurrentWindow(project)
    }

    companion object {
        private val shown = ConcurrentHashMap<Project, NewClaudeSessionPopup>()

        /** The popup shown in [project]'s window now, if one is. */
        fun shownIn(project: Project): NewClaudeSessionPopup? = shown[project]

        /**
         * Shows the popup in [project]'s window: below [button], the one clicked to open it, or else, as when the action
         * was found by its name, in the middle of the window.
         */
        fun open(project: Project, button: Component?) {
            NewClaudeSessionPopup(project).show(button)
        }
    }
}
