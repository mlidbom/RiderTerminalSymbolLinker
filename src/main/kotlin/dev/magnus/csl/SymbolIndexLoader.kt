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
 * A successful build replaces the in-memory index and is written to the [SymbolIndexCache]; then we
 * ask [TerminalLinks] to re-highlight existing terminal output so symbols printed before they were
 * known light up too. A failed/empty build (MCP unreachable, or ReSharper still loading the solution)
 * is left strictly alone — it never overwrites a good index or cache with nothing. Clicks resolve live
 * regardless, so navigation is never stale.
 */
object SymbolIndexLoader {
    private const val RETRY_INTERVAL_MS = 8_000L
    private const val MAX_STARTUP_ATTEMPTS = 40 // ~5 min of waiting for a large solution to warm up

    /**
     * Startup path: serve the cached index immediately (so links work without waiting on enumeration),
     * then build fresh in the background — retrying until ReSharper can answer, because a large
     * solution often isn't loaded yet when this runs.
     */
    fun loadAndRefresh(project: Project) {
        SymbolIndexCache.load(project)?.let { cached ->
            SymbolIndex.getInstance(project).set(cached)
            TerminalLinks.rehighlightExistingOutput()
        }
        build(project, notifyWhenDone = false, waitForReady = true)
    }

    fun refresh(project: Project, notifyWhenDone: Boolean) {
        build(project, notifyWhenDone, waitForReady = false)
    }

    private fun build(project: Project, notifyWhenDone: Boolean, waitForReady: Boolean) {
        ProgressManager.getInstance().run(
            object : Task.Backgroundable(project, "Loading C# symbols for terminal links", true) {
                override fun run(indicator: ProgressIndicator) {
                    val symbols = enumerate(project, indicator, waitForReady)
                    if (symbols == null) {
                        if (notifyWhenDone) {
                            notify(project, "Couldn't load C# symbols — the ReSharper MCP is unreachable or still loading the solution. Try again shortly.", NotificationType.WARNING)
                        }
                        return
                    }
                    SymbolIndex.getInstance(project).set(symbols)
                    SymbolIndexCache.save(project, symbols)
                    TerminalLinks.rehighlightExistingOutput()
                    if (notifyWhenDone) {
                        notify(project, "Symbol links refreshed: ${symbols.short.size} symbols.", NotificationType.INFORMATION)
                    }
                }
            },
        )
    }

    /**
     * One enumeration attempt, or — when [waitForReady] — retry on the polling interval until the MCP
     * returns a non-empty index or we give up. A `null` result (MCP down, or ReSharper not done loading)
     * is what we retry through; requiring a resolved `solutionName` first avoids enumerating the wrong
     * open solution while `list_solutions` is still warming up.
     */
    private fun enumerate(project: Project, indicator: ProgressIndicator, waitForReady: Boolean): SymbolNames? {
        var attempt = 0
        while (true) {
            indicator.checkCanceled()
            val solutionName = McpSolution.getInstance(project).name()
            val symbols = solutionName?.let { ReSharperMcp.enumerateSymbolNames(indicator, it) }
            if (symbols != null) return symbols
            if (!waitForReady || ++attempt >= MAX_STARTUP_ATTEMPTS) return null
            indicator.text = "Waiting for ReSharper to finish loading the solution…"
            repeat((RETRY_INTERVAL_MS / 250).toInt()) {
                indicator.checkCanceled()
                Thread.sleep(250)
            }
        }
    }

    private fun notify(project: Project, message: String, type: NotificationType) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("RiderTerminalSymbolLinker")
            .createNotification(message, type)
            .notify(project)
    }
}
