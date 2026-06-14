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
 * Reloading the index updates the underline gate for *future* terminal output. Already-printed lines
 * keep their old highlighting — the reworked terminal exposes no stable API to re-run filters over
 * existing output — and clicks resolve live regardless, so navigation is never stale.
 */
object SymbolIndexLoader {
    fun refresh(project: Project, notifyWhenDone: Boolean) {
        ProgressManager.getInstance().run(
            object : Task.Backgroundable(project, "Loading C# symbols for Claude links", true) {
                override fun run(indicator: ProgressIndicator) {
                    val names = ReSharperMcp.enumerateSymbolNames(indicator)
                    if (names == null) {
                        if (notifyWhenDone) {
                            notify(project, "Couldn't reach the ReSharper MCP — C# symbols not refreshed.", NotificationType.WARNING)
                        }
                        return
                    }
                    SymbolIndex.getInstance(project).set(names)
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
