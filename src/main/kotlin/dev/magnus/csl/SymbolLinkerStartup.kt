package dev.magnus.csl

import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity

/**
 * Unmistakable "the plugin is alive" signal: a balloon on project open plus a log line.
 * Separates "plugin didn't load" from "plugin loaded but filter never called".
 */
class SymbolLinkerStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        LOG.warn("CSL-SPIKE startup: ProjectActivity ran for ${project.name}")
        NotificationGroupManager.getInstance()
            .getNotificationGroup("ClaudeSymbolLinker")
            .createNotification("Claude Symbol Linker spike: loaded ✅", NotificationType.INFORMATION)
            .notify(project)
    }

    companion object {
        private val LOG = Logger.getInstance("CSL-SPIKE")
    }
}
