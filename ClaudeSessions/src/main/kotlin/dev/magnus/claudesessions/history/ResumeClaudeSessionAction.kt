package dev.magnus.claudesessions.history

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.project.DumbAware

/** Opens the [ClaudeSessionHistoryPicker], to resume a session of the history in a tab of the Claude Sessions tool window. */
class ResumeClaudeSessionAction : AnAction(), DumbAware {
    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT

    override fun update(event: AnActionEvent) {
        event.presentation.isEnabledAndVisible = event.project != null
    }

    override fun actionPerformed(event: AnActionEvent) {
        ClaudeSessionHistoryPicker.open(event.project ?: return)
    }

    companion object {
        /** The action's id in plugin.xml. */
        const val ID = "ClaudeSessions.ResumeSession"
    }
}
