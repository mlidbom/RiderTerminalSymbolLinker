package dev.magnus.csl

import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.openapi.progress.ProgressManager
import com.intellij.openapi.progress.Task
import com.intellij.openapi.project.Project

/**
 * Builds (or rebuilds) a project's [SymbolIndex] by enumerating solution symbols from the ReSharper
 * MCP on a background thread with a progress bar. Shared by [SymbolIndexStartup] (via [loadAndRefresh],
 * on solution open) and [RefreshSymbolsAction] (user-triggered, with a completion balloon).
 *
 * Each fresh build is written to the [SymbolIndexCache] and replaces the in-memory index. After the
 * index updates we ask [TerminalLinks] to re-highlight existing terminal output, so symbols printed
 * before they were known (e.g. a class Claude just created and then described) light up too — not just
 * future output. Clicks resolve live regardless, so navigation is never stale.
 */
object SymbolIndexLoader {
    /**
     * Startup path: serve the cached index immediately (so links work without waiting on the ~15s
     * enumeration), then rebuild fresh in the background to pick up any changes since it was written.
     */
    fun loadAndRefresh(project: Project) {
        SymbolIndexCache.load(project)?.let { cached ->
            SymbolIndex.getInstance(project).set(cached)
            TerminalLinks.rehighlightExistingOutput()
        }
        refresh(project, notifyWhenDone = false)
    }

    fun refresh(project: Project, notifyWhenDone: Boolean) {
        ProgressManager.getInstance().run(
            object : Task.Backgroundable(project, "Loading C# symbols for terminal links", true) {
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
                    SymbolIndexCache.save(project, names)
                    TerminalLinks.rehighlightExistingOutput()
                    if (notifyWhenDone) {
                        notify(project, "Symbol links refreshed: ${names.size} symbols.", NotificationType.INFORMATION)
                    }
                }
            },
        )
    }

    private fun notify(project: Project, message: String, type: NotificationType) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("RiderTerminalSymbolLinker")
            .createNotification(message, type)
            .notify(project)
    }
}
