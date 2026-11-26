package dev.magnus.csl

import com.intellij.execution.filters.HyperlinkInfoBase
import com.intellij.ide.actions.searcheverywhere.SearchEverywhereManager
import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.actionSystem.ActionUiKind
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.impl.SimpleDataContext
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.project.Project
import com.intellij.ui.awt.RelativePoint

/**
 * A link to C# code that the .NET Analysis&Refactoring MCP resolves when it's clicked: [SymbolHyperlinkInfo] for a type
 * or member, [NamespaceHyperlinkInfo] for a namespace. It asks the MCP off the EDT, then leads to the declarations it
 * found ([leadTo]). When the MCP can't be reached, it opens Search Everywhere for [name], so that a click is never a
 * dead end.
 *
 * The terminal gives a [HyperlinkInfoBase] the place in its output that was clicked, where a picker opens.
 */
abstract class HyperlinkInfoResolvedByTheMcp(protected val name: String) : HyperlinkInfoBase() {

    override fun navigate(project: Project, hyperlinkLocationPoint: RelativePoint?) {
        ApplicationManager.getApplication().executeOnPooledThread {
            val declarations = resolve(project, McpSolution.getInstance(project).name())
            ApplicationManager.getApplication().invokeLater {
                if (declarations == null) fallBackToSearchEverywhere(project) else leadTo(project, declarations, hyperlinkLocationPoint)
            }
        }
    }

    /** The declarations the link leads to, off the EDT. `null` = the MCP is unreachable or couldn't answer. */
    protected abstract fun resolve(project: Project, solutionName: String?): List<SymbolHit>?

    /** Opens [declarations], or shows a picker of them at [clickedAt], on the EDT. */
    protected abstract fun leadTo(project: Project, declarations: List<SymbolHit>, clickedAt: RelativePoint?)

    /** Opens [declaration], taking focus when [requestFocus], or tells that it's gone. */
    protected fun open(project: Project, declaration: SymbolHit, requestFocus: Boolean = true) {
        if (!declaration.open(project, requestFocus)) tellNothingFound(project)
    }

    /**
     * Shows a [LinkPicker] of [declarations] at [clickedAt], titled [title], with [note] above them, marking those whose
     * files are open.
     */
    protected fun showPicker(project: Project, title: String, note: String?, declarations: List<SymbolHit>, clickedAt: RelativePoint?) =
        LinkPicker.show(
            project, title, note, declarations, SymbolHit::pickerLabel, clickedAt,
            // Evaluated live (per paint) so a Ctrl-click open inside the picker marks the file at once.
            isEmphasized = { OpenFiles.normalize(it.file) in OpenFiles.paths(project) },
        ) { declaration, requestFocus -> open(project, declaration, requestFocus) }

    /** Tells the user, with [nothingFoundMessage], that the link leads nowhere. */
    protected fun tellNothingFound(project: Project) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("RiderTerminalSymbolLinker")
            .createNotification(nothingFoundMessage, NotificationType.INFORMATION)
            .notify(project)
        LinkClickOutcomes.report(LinkClickOutcome.NothingFound(nothingFoundMessage))
    }

    /** What [tellNothingFound] says. */
    protected abstract val nothingFoundMessage: String

    private fun fallBackToSearchEverywhere(project: Project) {
        val dataContext = SimpleDataContext.getProjectContext(project)
        val event = AnActionEvent.createEvent(dataContext, null, "RiderTerminalSymbolLinker", ActionUiKind.NONE, null)
        val searchEverywhere = SearchEverywhereManager.getInstance(project)
        try {
            searchEverywhere.show("SymbolSearchEverywhereContributor", name, event)
        } catch (_: Throwable) {
            searchEverywhere.show("SearchEverywhereContributor.All", name, event)
        }
        LinkClickOutcomes.report(LinkClickOutcome.FellBackToSearchEverywhere(name))
    }
}
