package dev.magnus.claudesessions.saving

import com.intellij.openapi.application.EDT
import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity
import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.api.LocalApi
import dev.magnus.claudesessions.hub.ClaudeToolingHub
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Brings back a project's saved Claude sessions as it opens: when tabs were saved, it has the IDE give the
 * [ClaudeSessionsView] its content at once, without waiting for the user to show the tool window, and the view brings
 * them back as it opens. With nothing saved, the view opens when the user first shows it.
 *
 * It also starts the IDE's [LocalApi], and its following of the [ClaudeToolingHub], unless an earlier project's start
 * did: so the hub has told which Claude Codes run by the time the view asks.
 */
class ClaudeSessionsStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        LocalApi.start()
        ClaudeToolingHub.getInstance()
        if (SavedClaudeSessions(project).read().tabs.isEmpty()) return
        withContext(Dispatchers.EDT) {
            ClaudeSessionsView.toolWindowOf(project)?.contentManager // The IDE gives a tool window its content as it's asked for.
        }
    }
}
