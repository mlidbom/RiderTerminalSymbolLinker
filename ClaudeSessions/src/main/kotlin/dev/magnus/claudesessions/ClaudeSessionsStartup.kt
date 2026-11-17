package dev.magnus.claudesessions

import com.intellij.openapi.application.EDT
import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Resumes a project's saved Claude sessions as it opens: when sessions were saved, it has the IDE give the
 * [ClaudeSessionsView] its content at once, without waiting for the user to show the tool window, and the view resumes
 * them as it opens. With nothing saved, the view opens when the user first shows it.
 */
class ClaudeSessionsStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        if (SavedClaudeSessions(project).read().sessions.isEmpty()) return
        withContext(Dispatchers.EDT) {
            ClaudeSessionsView.toolWindowOf(project)?.contentManager // The IDE gives a tool window its content as it's asked for.
        }
    }
}
