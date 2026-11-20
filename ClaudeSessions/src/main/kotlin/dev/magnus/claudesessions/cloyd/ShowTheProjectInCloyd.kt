package dev.magnus.claudesessions.cloyd

import com.intellij.notification.NotificationType
import com.intellij.openapi.diagnostic.thisLogger
import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity
import dev.magnus.claudesessions.ClaudeSessionsNotifications
import java.nio.file.Path
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runInterruptible

/**
 * As a project opens, has Cloyd show its window, through the [CloydCommand]: Cloyd, the application beside the IDEs,
 * shows each project's Claude Code sessions in a window of its own. Its outcome goes to the IDE's log, and a notification
 * tells the user when the window doesn't show, and why.
 */
class ShowTheProjectInCloyd : ProjectActivity {
    override suspend fun execute(project: Project) {
        val folder = Path.of(project.basePath ?: error("The project ${project.name} has no folder for Cloyd to show."))
        val outcome = runInterruptible(Dispatchers.IO) { CloydCommand(CloydsFolder.ofTheIdesEnvironment()).showTheWindowOf(folder) }
        thisLogger().info("Cloyd, asked to show $folder: ${outcome.description}")
        if (outcome != CloydCommandOutcome.Shown)
            ClaudeSessionsNotifications.tell(project, "Cloyd doesn't show the project's Claude Code sessions", outcome.description, NotificationType.WARNING)
    }
}
