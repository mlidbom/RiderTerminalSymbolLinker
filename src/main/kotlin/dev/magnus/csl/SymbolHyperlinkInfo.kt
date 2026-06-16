package dev.magnus.csl

import com.intellij.execution.filters.HyperlinkInfo
import com.intellij.ide.actions.searcheverywhere.SearchEverywhereManager
import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.actionSystem.ActionUiKind
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.impl.SimpleDataContext
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.LocalFileSystem

/**
 * On click, resolve [token] to its declaration(s) via the ReSharper MCP (exact by name):
 *  - one declaration  -> jump straight to its file:line
 *  - several          -> small picker of qualified locations
 *  - none             -> brief "no symbol" notice
 *  - MCP unreachable   -> fall back to Search Everywhere (so a click is never a dead end)
 *
 * In the picker, a plain click (or Enter) opens the hit and closes the list, as usual. A
 * Ctrl-click (Cmd-click on macOS) instead opens the hit in the background and leaves the list
 * open, so several candidate declarations can be opened in turn before dismissing it.
 */
class SymbolHyperlinkInfo(private val token: String) : HyperlinkInfo {

    override fun navigate(project: Project) {
        ApplicationManager.getApplication().executeOnPooledThread {
            val solutionName = McpSolution.getInstance(project).name()
            val hits = resolve(solutionName)
            ApplicationManager.getApplication().invokeLater {
                when {
                    hits == null -> fallbackToSearchEverywhere(project)
                    hits.size == 1 -> openHit(project, hits.first())
                    hits.size > 1 -> showPicker(project, hits)
                    else -> notifyNoSymbol(project)
                }
            }
        }
    }

    /**
     * Resolve [token] by name. For a combined `Type.Member` token, try the qualified name first; if the
     * backend can't resolve that form (empty, not unreachable) fall back to the bare member name — the
     * same hits a click on the member alone would have produced, just now under one combined link.
     * `null` (MCP unreachable) is propagated unchanged so the caller can fall back to Search Everywhere.
     */
    private fun resolve(solutionName: String?): List<SymbolHit>? {
        val hits = ReSharperMcp.goToDefinition(token, solutionName)
        if (hits != null && hits.isEmpty() && '.' in token) {
            return ReSharperMcp.goToDefinition(token.substringAfterLast('.'), solutionName)
        }
        return hits
    }

    /** [requestFocus] false opens the editor in the background, leaving focus where it is (the picker). */
    private fun openHit(project: Project, hit: SymbolHit, requestFocus: Boolean = true) {
        val file = LocalFileSystem.getInstance().findFileByPath(hit.file.replace('\\', '/'))
        if (file != null) {
            OpenFileDescriptor(project, file, (hit.line - 1).coerceAtLeast(0), 0).navigate(requestFocus)
        } else {
            notifyNoSymbol(project)
        }
    }

    private fun showPicker(project: Project, hits: List<SymbolHit>) =
        LinkPicker.show(
            project, "Symbols matching \"$token\"", hits, ::pickerLabel,
            // Evaluated live (per paint) so a Ctrl-click open inside the picker marks the file at once.
            isEmphasized = { OpenFiles.normalize(it.file) in OpenFiles.paths(project) },
        ) { hit, requestFocus -> openHit(project, hit, requestFocus) }

    private fun pickerLabel(hit: SymbolHit): String =
        "${hit.kind} ${hit.name}  —  ${hit.file.substringAfterLast('\\')}:${hit.line}"

    private fun fallbackToSearchEverywhere(project: Project) {
        val dataContext = SimpleDataContext.getProjectContext(project)
        val event = AnActionEvent.createEvent(dataContext, null, "RiderTerminalSymbolLinker", ActionUiKind.NONE, null)
        val searchEverywhere = SearchEverywhereManager.getInstance(project)
        try {
            searchEverywhere.show("SymbolSearchEverywhereContributor", token, event)
        } catch (_: Throwable) {
            searchEverywhere.show("SearchEverywhereContributor.All", token, event)
        }
    }

    private fun notifyNoSymbol(project: Project) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("RiderTerminalSymbolLinker")
            .createNotification("No symbol \"$token\" found", NotificationType.INFORMATION)
            .notify(project)
    }
}
