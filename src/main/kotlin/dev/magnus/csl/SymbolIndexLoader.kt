package dev.magnus.csl

import com.intellij.notification.Notification
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
 *
 * Notifications are governed by [Announce]: the first run with no cache ([Announce.COLD_START]) shows a
 * sticky "symbols are loading — links won't work yet" balloon up front, because that is the one case
 * where the user is staring at a terminal whose links silently do nothing. A warm start stays silent
 * (links already work from the cache); a manual refresh reports its result.
 */
object SymbolIndexLoader {
    private const val RETRY_INTERVAL_MS = 8_000L
    private const val MAX_STARTUP_ATTEMPTS = 40 // ~5 min of waiting for a large solution to warm up

    /** Which balloons a [build] emits — see the class doc. */
    private enum class Announce { SILENT, REFRESH_RESULT, COLD_START }

    /**
     * Startup path: serve the cached index immediately (so links work without waiting on enumeration),
     * then build fresh in the background — retrying until ReSharper can answer, because a large
     * solution often isn't loaded yet when this runs. With no cache, links can't work until the build
     * finishes, so we announce that visibly ([Announce.COLD_START]); with a cache we refresh silently.
     */
    fun loadAndRefresh(project: Project) {
        val cached = SymbolIndexCache.load(project)
        if (cached != null) {
            SymbolIndex.getInstance(project).set(cached)
            TerminalLinks.rehighlightExistingOutput()
        }
        val announce = if (cached != null) Announce.SILENT else Announce.COLD_START
        build(project, announce, waitForReady = true)
    }

    fun refresh(project: Project, notifyWhenDone: Boolean) {
        build(project, if (notifyWhenDone) Announce.REFRESH_RESULT else Announce.SILENT, waitForReady = false)
    }

    private fun build(project: Project, announce: Announce, waitForReady: Boolean) {
        ProgressManager.getInstance().run(
            object : Task.Backgroundable(project, "Loading .NET symbols for terminal links", true) {
                override fun run(indicator: ProgressIndicator) {
                    indicator.text = "Indexing .NET symbols — terminal links activate when this finishes"
                    val loadingNotice = if (announce == Announce.COLD_START) showLoadingNotice(project) else null
                    val symbols = enumerate(project, indicator, waitForReady)
                    loadingNotice?.expire()
                    if (symbols == null) {
                        when (announce) {
                            Announce.COLD_START -> notify(project, "Couldn't load .NET symbols for terminal links — the ReSharper MCP is unreachable or still loading the solution. Terminal links stay inactive; use “Refresh Terminal Links” in the terminal menu to retry.", NotificationType.WARNING)
                            Announce.REFRESH_RESULT -> notify(project, "Couldn't refresh .NET symbol links — the ReSharper MCP is unreachable or still loading the solution. Try again shortly.", NotificationType.WARNING)
                            Announce.SILENT -> {}
                        }
                        return
                    }
                    SymbolIndex.getInstance(project).set(symbols)
                    SymbolIndexCache.save(project, symbols)
                    TerminalLinks.rehighlightExistingOutput()
                    when (announce) {
                        Announce.COLD_START -> notify(project, "Terminal links are active — ${symbols.short.size} .NET symbols indexed. Symbol names in terminal output are now clickable.", NotificationType.INFORMATION)
                        Announce.REFRESH_RESULT -> notify(project, "Symbol links refreshed: ${symbols.short.size} symbols.", NotificationType.INFORMATION)
                        Announce.SILENT -> {}
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
            indicator.text = "Waiting for ReSharper to finish loading the solution… (terminal links activate once it does)"
            repeat((RETRY_INTERVAL_MS / 250).toInt()) {
                indicator.checkCanceled()
                Thread.sleep(250)
            }
        }
    }

    /**
     * The sticky first-run notice, returned so the caller can [Notification.expire] it the moment the
     * build resolves (success or failure) — the user never has to dismiss it by hand.
     */
    private fun showLoadingNotice(project: Project): Notification =
        NotificationGroupManager.getInstance()
            .getNotificationGroup("RiderTerminalSymbolLinker.Startup")
            .createNotification(
                ".NET terminal symbol links are loading",
                "Building the symbol index for this solution. Symbol names in terminal output won't be clickable until this finishes — the first time can take a few minutes for a large solution.",
                NotificationType.INFORMATION,
            )
            .also { it.notify(project) }

    private fun notify(project: Project, message: String, type: NotificationType) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("RiderTerminalSymbolLinker")
            .createNotification(message, type)
            .notify(project)
    }
}
