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
import com.intellij.openapi.ui.popup.PopupChooserBuilder
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.ui.SimpleListCellRenderer
import com.intellij.ui.components.JBList
import java.awt.Toolkit
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import javax.swing.ListSelectionModel

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
            val hits = ReSharperMcp.goToDefinition(token, solutionName)
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

    /** [requestFocus] false opens the editor in the background, leaving focus where it is (the picker). */
    private fun openHit(project: Project, hit: SymbolHit, requestFocus: Boolean = true) {
        val file = LocalFileSystem.getInstance().findFileByPath(hit.file.replace('\\', '/'))
        if (file != null) {
            OpenFileDescriptor(project, file, (hit.line - 1).coerceAtLeast(0), 0).navigate(requestFocus)
        } else {
            notifyNoSymbol(project)
        }
    }

    private fun showPicker(project: Project, hits: List<SymbolHit>) {
        val list = JBList(hits).apply { selectionMode = ListSelectionModel.SINGLE_SELECTION }
        // Ctrl/Cmd-click opens a hit without closing the list. The builder's own click handler already
        // ignores clicks with that modifier (it treats them as a list toggle, not an activation), so it
        // won't close the popup — we just open the row under the cursor, keeping focus on the list.
        list.addMouseListener(object : MouseAdapter() {
            override fun mouseClicked(e: MouseEvent) {
                val keepOpenModifier = Toolkit.getDefaultToolkit().menuShortcutKeyMaskEx
                if (e.button != MouseEvent.BUTTON1 || e.modifiersEx and keepOpenModifier == 0) return
                val index = list.locationToIndex(e.point)
                if (index < 0 || !list.getCellBounds(index, index).contains(e.point)) return
                openHit(project, list.model.getElementAt(index), requestFocus = false)
                e.consume()
            }
        })

        PopupChooserBuilder(list)
            .setTitle("Symbols matching \"$token\"")
            .setRenderer(SimpleListCellRenderer.create("") { hit -> pickerLabel(hit) })
            .setItemChosenCallback { openHit(project, it) }
            // Always-visible search field on top, like Go to Symbol; filters on the visible row text.
            .setNamerForFiltering { pickerLabel(it) }
            .setFilterAlwaysVisible(true)
            .createPopup()
            .showCenteredInCurrentWindow(project)
    }

    private fun pickerLabel(hit: SymbolHit): String =
        "${hit.kind} ${hit.name}  —  ${hit.file.substringAfterLast('\\')}:${hit.line}"

    private fun fallbackToSearchEverywhere(project: Project) {
        val dataContext = SimpleDataContext.getProjectContext(project)
        val event = AnActionEvent.createFromDataContext("RiderTerminalSymbolLinker", null, dataContext)
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
