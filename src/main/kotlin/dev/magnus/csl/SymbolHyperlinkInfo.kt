package dev.magnus.csl

import com.intellij.execution.filters.HyperlinkInfo
import com.intellij.ide.actions.searcheverywhere.SearchEverywhereManager
import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.impl.SimpleDataContext
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.popup.JBPopupFactory
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.ui.SimpleListCellRenderer

/**
 * On click, resolve [token] against the ReSharper MCP (reliable, ReSharper-backed):
 *  - exactly one match  -> jump straight to its file:line
 *  - several matches     -> small picker of locations
 *  - none                -> brief "no symbol" notice
 *  - MCP unreachable      -> fall back to Search Everywhere (so a click is never a dead end)
 */
class SymbolHyperlinkInfo(private val token: String) : HyperlinkInfo {

    override fun navigate(project: Project) {
        ApplicationManager.getApplication().executeOnPooledThread {
            val hits = ReSharperMcp.searchSymbol(token)
            val matches = when {
                hits == null -> null
                token.contains('.') -> hits // dot-qualified: trust the MCP's ContainingType.Member matching
                else -> hits.filter { it.name == token } // bare name: exact matches only
            }
            ApplicationManager.getApplication().invokeLater {
                when {
                    matches == null -> fallbackToSearchEverywhere(project)
                    matches.size == 1 -> openHit(project, matches.first())
                    matches.size > 1 -> showPicker(project, matches)
                    else -> notifyNoSymbol(project)
                }
            }
        }
    }

    private fun openHit(project: Project, hit: SymbolHit) {
        val file = LocalFileSystem.getInstance().findFileByPath(hit.file.replace('\\', '/'))
        if (file != null) {
            OpenFileDescriptor(project, file, (hit.line - 1).coerceAtLeast(0), 0).navigate(true)
        } else {
            notifyNoSymbol(project)
        }
    }

    private fun showPicker(project: Project, hits: List<SymbolHit>) {
        JBPopupFactory.getInstance()
            .createPopupChooserBuilder(hits)
            .setTitle("Symbols matching \"$token\"")
            .setRenderer(
                SimpleListCellRenderer.create("") { hit ->
                    "${hit.kind} ${hit.name}  —  ${hit.file.substringAfterLast('\\')}:${hit.line}"
                },
            )
            .setItemChosenCallback { openHit(project, it) }
            .createPopup()
            .showCenteredInCurrentWindow(project)
    }

    private fun fallbackToSearchEverywhere(project: Project) {
        val dataContext = SimpleDataContext.getProjectContext(project)
        val event = AnActionEvent.createFromDataContext("ClaudeSymbolLinker", null, dataContext)
        val searchEverywhere = SearchEverywhereManager.getInstance(project)
        try {
            searchEverywhere.show("SymbolSearchEverywhereContributor", token, event)
        } catch (_: Throwable) {
            searchEverywhere.show("SearchEverywhereContributor.All", token, event)
        }
    }

    private fun notifyNoSymbol(project: Project) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("ClaudeSymbolLinker")
            .createNotification("No symbol \"$token\" found", NotificationType.INFORMATION)
            .notify(project)
    }
}
