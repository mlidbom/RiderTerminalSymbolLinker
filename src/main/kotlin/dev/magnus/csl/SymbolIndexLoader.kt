package dev.magnus.csl

import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.openapi.progress.ProgressManager
import com.intellij.openapi.progress.Task
import com.intellij.openapi.project.Project

/**
 * Builds (or rebuilds) a project's [SymbolIndex] by enumerating solution symbols from the ReSharper
 * MCP on a background thread with a progress bar. Shared by [SymbolIndexStartup] (silent, on solution
 * open) and [RefreshSymbolsAction] (user-triggered, with a completion balloon).
 *
 * After the index updates we ask [TerminalLinks] to re-highlight existing terminal output, so symbols
 * printed before they were known (e.g. a class Claude just created and then described) light up too —
 * not just future output. Clicks resolve live regardless, so navigation is never stale.
 */
object SymbolIndexLoader {
    fun refresh(project: Project, notifyWhenDone: Boolean) {
        ProgressManager.getInstance().run(
            object : Task.Backgroundable(project, "Loading C# symbols for Claude links", true) {
                override fun run(indicator: ProgressIndicator) {
                    val solutionName = McpSolution.getInstance(project).name()
                    val names = ReSharperMcp.enumerateSymbolNames(indicator, solutionName)
                    if (names == null) {
                        if (notifyWhenDone) {
                            notify(project, "Couldn't reach the ReSharper MCP — C# symbols not refreshed.", NotificationType.WARNING)
                        }
                        return
                    }
                    SymbolIndex.getInstance(project).set(names)
                    TerminalLinks.rehighlightExistingOutput()
                    if (notifyWhenDone) {
                        notify(project, "Claude symbol links refreshed: ${names.size} symbols.", NotificationType.INFORMATION)
                    }
                }
            },
        )
    }

    private fun notify(project: Project, message: String, type: NotificationType) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("ClaudeSymbolLinker")
            .createNotification(message, type)
            .notify(project)
    }
}
