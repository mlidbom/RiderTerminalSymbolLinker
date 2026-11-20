package dev.magnus.claudesessions.windows

import com.intellij.ide.AppLifecycleListener
import com.intellij.openapi.diagnostic.thisLogger
import com.intellij.openapi.project.ProjectManager

/**
 * As the IDE exits or restarts, while the projects' windows are still shown, records the virtual desktop each is on,
 * for [PutTheWindowBackOnItsDesktop] as the project opens again: see [DesktopsThroughARestart].
 */
class RecordTheWindowsDesktopsAsTheIdeExits : AppLifecycleListener {
    override fun appClosing() {
        thisLogger().info("The IDE is closing: recording the virtual desktop of each project's window.")
        DesktopsThroughARestart.record(ProjectManager.getInstance().openProjects.toList())
    }
}
