package dev.magnus.csl

import com.intellij.execution.filters.HyperlinkInfo
import com.intellij.ide.actions.searcheverywhere.SearchEverywhereManager
import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.impl.SimpleDataContext
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.project.Project

/**
 * On click: (1) fire a balloon so we can confirm the hyperlink actually triggered, and
 * (2) best-effort open Search Everywhere seeded with the token, which is the eventual
 * navigation path. The balloon alone answers the spike's core question even if the
 * Search Everywhere call needs API tweaks for Rider.
 */
class SymbolHyperlinkInfo(private val symbol: String) : HyperlinkInfo {
    override fun navigate(project: Project) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("ClaudeSymbolLinker")
            .createNotification("Claude Symbol Linker: clicked \"$symbol\"", NotificationType.INFORMATION)
            .notify(project)

        ApplicationManager.getApplication().invokeLater {
            try {
                val dataContext = SimpleDataContext.getProjectContext(project)
                val event = AnActionEvent.createFromDataContext("ClaudeSymbolLinker", null, dataContext)
                SearchEverywhereManager.getInstance(project)
                    .show("SearchEverywhereContributor.All", symbol, event)
            } catch (_: Throwable) {
                // Spike: the balloon already proves the click fired.
            }
        }
    }
}
