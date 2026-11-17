package dev.magnus.claudesessions

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.project.DumbAware

/** Starts a new Claude Code session in a tab of the [ClaudeSessionsView], in the project's folder. */
class NewClaudeSessionAction : AnAction(), DumbAware {
    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT

    override fun update(event: AnActionEvent) {
        event.presentation.isEnabledAndVisible = event.project != null
    }

    override fun actionPerformed(event: AnActionEvent) {
        val project = event.project ?: return
        val view = ClaudeSessionsView.getInstance(project)
        if (!view.isOpen) ClaudeSessionsView.toolWindowOf(project)?.contentManager // Has the IDE give the tool window its content.
        view.startNewSession()
    }

    companion object {
        /** The action's id in plugin.xml. */
        const val ID = "ClaudeSessions.NewSession"
    }
}
