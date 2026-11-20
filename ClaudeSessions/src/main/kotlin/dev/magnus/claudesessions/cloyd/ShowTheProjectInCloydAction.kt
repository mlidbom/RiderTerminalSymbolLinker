package dev.magnus.claudesessions.cloyd

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.project.DumbAwareAction

/**
 * Tools > Show the Project in Cloyd, Ctrl+Alt+Shift+Y: the way from the IDE to the project's window in Cloyd, which
 * brings it forward, or opens it when it was closed, as opening the project does ([ProjectWindowInCloyd]).
 */
class ShowTheProjectInCloydAction : DumbAwareAction() {
    override fun getActionUpdateThread() = ActionUpdateThread.BGT

    override fun update(event: AnActionEvent) {
        event.presentation.isEnabled = event.project?.basePath != null
    }

    override fun actionPerformed(event: AnActionEvent) {
        val project = event.project ?: return
        ApplicationManager.getApplication().executeOnPooledThread { ProjectWindowInCloyd(project).show() }
    }
}
