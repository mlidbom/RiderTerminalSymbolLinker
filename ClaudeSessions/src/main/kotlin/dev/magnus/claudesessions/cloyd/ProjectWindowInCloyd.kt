package dev.magnus.claudesessions.cloyd

import com.intellij.notification.NotificationType
import com.intellij.openapi.diagnostic.thisLogger
import com.intellij.openapi.project.Project
import dev.magnus.claudesessions.ClaudeSessionsNotifications
import java.nio.file.Path

/**
 * The window of [project] in Cloyd, the application beside the IDEs that shows each project's Claude Code sessions in a
 * window of its own.
 */
class ProjectWindowInCloyd(private val project: Project) {
    /**
     * Has Cloyd show the window, through the [CloydCommand]: brought forward when it's open, else opened. Its outcome goes
     * to the IDE's log, and a notification tells the user when the window doesn't show, and why. It waits for the command,
     * so it runs off the EDT.
     */
    fun show() {
        val folder = Path.of(project.basePath ?: error("The project ${project.name} has no folder for Cloyd to show."))
        val outcome = CloydCommand(CloydsFolder.ofTheIdesEnvironment()).showTheWindowOf(folder)
        thisLogger().info("Cloyd, asked to show $folder: ${outcome.description}")
        if (outcome != CloydCommandOutcome.Shown)
            ClaudeSessionsNotifications.tell(project, "Cloyd doesn't show the project's Claude Code sessions", outcome.description, NotificationType.WARNING)
    }
}
