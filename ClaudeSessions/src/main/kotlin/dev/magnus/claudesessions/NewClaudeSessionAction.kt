package dev.magnus.claudesessions

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.project.DumbAware

/**
 * Opens the [NewClaudeSessionPopup], below the button clicked, to start a new Claude Code session, named or not, in a tab
 * of the Claude Sessions tool window, in the project's folder.
 */
class NewClaudeSessionAction : AnAction(), DumbAware {
    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT

    override fun update(event: AnActionEvent) {
        event.presentation.isEnabledAndVisible = event.project != null
    }

    override fun actionPerformed(event: AnActionEvent) {
        NewClaudeSessionPopup.open(event.project ?: return, event.inputEvent?.component)
    }

    companion object {
        /** The action's id in plugin.xml. */
        const val ID = "ClaudeSessions.NewSession"
    }
}
