package dev.magnus.claudesessions.windows

import com.intellij.openapi.project.Project
import com.intellij.openapi.project.ProjectManager
import com.intellij.openapi.wm.WindowManager
import java.awt.KeyboardFocusManager
import java.awt.Window
import java.nio.file.Path

/**
 * The window of a project open in the IDE, its frame: the project's folder, the window's title, its native handle, and
 * whether it, or a dialog it owns, has the input focus.
 */
data class ProjectWindow(val project: Path, val title: String, val handle: WindowHandle, val hasTheInputFocus: Boolean) {
    /** The virtual desktop the window is on, or null when it's on none. */
    val desktop: VirtualDesktopId? get() = VirtualDesktops.desktopOf(handle)

    companion object {
        /** The windows of the projects open in the IDE that are shown. On the EDT. */
        fun ofTheOpenProjects(): List<ProjectWindow> = ProjectManager.getInstance().openProjects.mapNotNull(::of)

        /** The window of [project], if it's shown. On the EDT. */
        fun of(project: Project): ProjectWindow? {
            val frame = WindowManager.getInstance().getFrame(project)?.takeIf { it.isShowing } ?: return null
            //todo: remove hasTheInputFocus, here and in LocalApi's GET /windows, once the hub finds whether a Rider window has
            //   the focus itself, from Windows' foreground window (the tooling owner, 2026-11-20): nothing else reads it. A
            //   freshly started sandbox Rider said true by Java's activeWindow, and it isn't known whether its window was
            //   in the foreground then.
            return ProjectWindow(Path.of(project.basePath!!), frame.title.orEmpty(), WindowHandle.of(frame), frame.hasTheInputFocus)
        }

        /** Whether the window that has the input focus is this one, or a dialog it owns. */
        private val Window.hasTheInputFocus: Boolean
            get() = generateSequence(KeyboardFocusManager.getCurrentKeyboardFocusManager().activeWindow) { it.owner }.any { it == this }
    }
}
