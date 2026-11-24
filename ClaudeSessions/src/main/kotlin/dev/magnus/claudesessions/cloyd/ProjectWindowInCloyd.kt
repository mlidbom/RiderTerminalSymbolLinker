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
     * Has Cloyd show the window, through the [CloydCommand], telling it [why]: brought forward when it's open, else opened,
     * unless Cloyd leaves it closed, as while its Projects Dashboard is open for [WhyTheWindowIsAskedFor.IDE_OPENED_THE_PROJECT].
     * Its outcome goes to the IDE's log, and a notification tells the user when the command fails, and why. It waits for the
     * command, so it runs off the EDT.
     */
    fun show(why: WhyTheWindowIsAskedFor) {
        val folder = Path.of(project.basePath ?: error("The project ${project.name} has no folder for Cloyd to show."))
        val outcome = CloydCommand(CloydsFolder.ofTheIdesEnvironment()).showTheWindowOf(folder, why)
        thisLogger().info("Cloyd, asked to show $folder as ${why.inTheRequest}: ${outcome.description}")
        if (outcome != CloydCommandOutcome.Shown)
            ClaudeSessionsNotifications.tell(project, "Cloyd doesn't show the project's Claude Code sessions", outcome.description, NotificationType.WARNING)
    }
}
