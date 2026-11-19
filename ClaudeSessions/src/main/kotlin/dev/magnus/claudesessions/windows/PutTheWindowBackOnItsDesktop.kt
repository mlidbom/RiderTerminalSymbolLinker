package dev.magnus.claudesessions.windows

import com.intellij.openapi.application.EDT
import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity
import com.intellij.openapi.wm.WindowManager
import com.intellij.util.ui.update.UiNotifyConnector
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * As a project opens, puts its window back on the virtual desktop it was on as the IDE last exited, if it was open then:
 * see [DesktopsThroughARestart].
 */
class PutTheWindowBackOnItsDesktop : ProjectActivity {
    override suspend fun execute(project: Project) {
        withContext(Dispatchers.EDT) {
            val frame = WindowManager.getInstance().getFrame(project) ?: return@withContext
            UiNotifyConnector.doWhenFirstShown(frame.rootPane) { DesktopsThroughARestart.putBack(project) }
        }
    }
}
