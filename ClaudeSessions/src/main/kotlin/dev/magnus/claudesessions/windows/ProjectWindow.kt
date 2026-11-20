package dev.magnus.claudesessions.windows

import com.intellij.openapi.project.Project
import com.intellij.openapi.project.ProjectManager
import com.intellij.openapi.wm.WindowManager
import java.nio.file.Path

/** The window of a project open in the IDE, its frame: the project's folder, the window's title, and its native handle. */
data class ProjectWindow(val project: Path, val title: String, val handle: WindowHandle) {
    /** The virtual desktop the window is on, or null when it's on none. */
    val desktop: VirtualDesktopId? get() = VirtualDesktops.desktopOf(handle)

    companion object {
        /** The windows of the projects open in the IDE that are shown. On the EDT. */
        fun ofTheOpenProjects(): List<ProjectWindow> = ProjectManager.getInstance().openProjects.mapNotNull(::of)

        /** The window of [project], if it's shown. On the EDT. */
        fun of(project: Project): ProjectWindow? {
            val frame = WindowManager.getInstance().getFrame(project)?.takeIf { it.isShowing } ?: return null
            return ProjectWindow(Path.of(project.basePath!!), frame.title.orEmpty(), WindowHandle.of(frame))
        }
    }
}
