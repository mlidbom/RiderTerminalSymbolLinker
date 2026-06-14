package dev.magnus.csl

import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.application.ReadAction
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.openapi.progress.ProgressManager
import com.intellij.openapi.progress.Task
import com.intellij.openapi.project.Project
import com.intellij.openapi.roots.ProjectFileIndex

/**
 * Builds (or rebuilds) a project's [SolutionFileNames] by walking the platform's content index — every
 * file under the solution's content roots — on a background thread. The walk happens in smart mode under
 * a read action; libraries and the SDK are excluded, so the result is exactly the solution's own files.
 *
 * Run at solution open (via [SolutionFileIndexStartup]) and on demand from [RefreshSymbolsAction]. After
 * a successful build we ask [TerminalLinks] to re-highlight existing terminal output so file references
 * printed before the index existed light up too — the same nudge the symbol side uses.
 *
 * Unlike the symbol index there is no disk cache: the content walk is local and fast, so there is nothing
 * slow to hide behind a cache. New files added mid-session aren't seen until the next build — refresh from
 * the terminal menu picks them up, mirroring the symbol index's staleness model.
 */
object SolutionFileIndexLoader {
    private val LOG = Logger.getInstance("CSL")

    fun loadAndRefresh(project: Project) = build(project, notifyWhenDone = false)

    fun refresh(project: Project, notifyWhenDone: Boolean) = build(project, notifyWhenDone)

    private fun build(project: Project, notifyWhenDone: Boolean) {
        ProgressManager.getInstance().run(
            object : Task.Backgroundable(project, "Indexing solution files for terminal links", true) {
                override fun run(indicator: ProgressIndicator) {
                    indicator.text = "Indexing solution files…"
                    val byName = enumerate(project)
                    SolutionFileNames.getInstance(project).set(byName)
                    LOG.info("CSL file index: ${byName.size} distinct names, ${byName.values.sumOf { it.size }} files")
                    TerminalLinks.rehighlightExistingOutput()
                    if (notifyWhenDone) {
                        val fileCount = byName.values.sumOf { it.size }
                        notify(project, "File links refreshed: $fileCount files.", NotificationType.INFORMATION)
                    }
                }
            },
        )
    }

    /** name(lowercased) -> full VFS paths, over every non-directory file in the solution's content.
     *  A non-blocking read action in smart mode: it yields to write actions (re-running if needed)
     *  rather than holding the whole content walk under one lock, and waits out indexing. */
    private fun enumerate(project: Project): Map<String, List<String>> =
        ReadAction.nonBlocking<Map<String, List<String>>> {
            val map = HashMap<String, MutableList<String>>()
            ProjectFileIndex.getInstance(project).iterateContent { file ->
                if (!file.isDirectory) map.getOrPut(file.name.lowercase()) { ArrayList() }.add(file.path)
                true
            }
            map
        }.inSmartMode(project).executeSynchronously()

    private fun notify(project: Project, message: String, type: NotificationType) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("RiderTerminalSymbolLinker")
            .createNotification(message, type)
            .notify(project)
    }
}
