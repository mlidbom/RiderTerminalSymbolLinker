package dev.magnus.claudesessions.windows

import com.intellij.ide.AppLifecycleListener
import com.intellij.openapi.project.ProjectManager

/**
 * As the IDE exits or restarts, while the projects' windows are still shown, records the virtual desktop each is on,
 * for [PutTheWindowBackOnItsDesktop] as the project opens again: see [DesktopsThroughARestart].
 */
class RecordTheWindowsDesktopsAsTheIdeExits : AppLifecycleListener {
    override fun appWillBeClosed(isRestart: Boolean) {
        DesktopsThroughARestart.record(ProjectManager.getInstance().openProjects.toList())
    }
}
