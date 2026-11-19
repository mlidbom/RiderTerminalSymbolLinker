package dev.magnus.claudesessions.saving

import com.intellij.ide.AppLifecycleListener
import com.intellij.openapi.project.Project
import com.intellij.openapi.project.ProjectCloseListener
import com.intellij.openapi.project.ProjectManager
import dev.magnus.claudesessions.ClaudeSessionsView

/**
 * Has each project's [ClaudeSessionKeeper] save the sessions one last time and stop as the project closes, or the IDE
 * exits, before the IDE ends the processes of the [ClaudeSessionsView]'s tabs: the sessions saved are then those that
 * ran.
 */
class SaveTheSessionsAsTheProjectCloses : ProjectCloseListener, AppLifecycleListener {
    override fun projectClosing(project: Project) {
        ClaudeSessionKeeper.getInstance(project).saveOneLastTimeAndStop()
    }

    override fun appWillBeClosed(isRestart: Boolean) {
        ProjectManager.getInstance().openProjects.forEach(::projectClosing)
    }
}
