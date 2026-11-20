package dev.magnus.claudesessions.windows

import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.diagnostic.thisLogger
import com.intellij.openapi.project.Project
import java.nio.file.Files
import java.nio.file.Path
import java.util.UUID
import java.util.concurrent.CopyOnWriteArrayList

/**
 * The virtual desktop each project's window was on as the IDE exited, so that its window goes back to it once the
 * project opens again, after every restart: Windows opens each window on the desktop shown then. Magnus, asked whether
 * after every restart or only after the Rider restart tool's (2026-11-19): "Every restart please :)".
 *
 * A file for each project, in the IDE's system folder, `claude-sessions/desktops/<the project's location hash>.json`,
 * written as the IDE exits, and read and deleted as the project opens.
 */
object DesktopsThroughARestart {
    private val folder: Path get() = Path.of(PathManager.getSystemPath(), "claude-sessions", "desktops")

    private val windowsPutBack = CopyOnWriteArrayList<Map<String, String>>()

    /**
     * The windows [putBack] has put back on their desktops since the plugin started, each with its project's folder and
     * the desktop: what the development observation channel tells of it.
     */
    val windowsPutBackOnTheirDesktops: List<Map<String, String>> get() = windowsPutBack.toList()

    /** Records the desktop each of [projects]' windows is on, as the IDE is about to exit. On the EDT. */
    fun record(projects: List<Project>) {
        Files.createDirectories(folder)
        for (project in projects) {
            val window = ProjectWindow.of(project)
            val desktop = window?.desktop
            if (desktop == null) {
                thisLogger().info("Recorded no virtual desktop for the window of ${project.name}: ${if (window == null) "it isn't shown" else "it's on none"}.")
                continue
            }
            Files.writeString(fileOf(project), JsonObject().apply { addProperty("desktop", desktop.value.toString()) }.toString())
            thisLogger().info("Recorded the virtual desktop ${desktop.value} of the window of ${project.name}, for it to go back to as the project opens again.")
        }
    }

    /**
     * Moves [project]'s window back to the desktop it was on as the IDE exited, if that was recorded, and the desktop
     * still exists. On the EDT, once the window is shown.
     */
    fun putBack(project: Project) {
        val file = fileOf(project)
        if (!Files.exists(file)) return
        val desktop = VirtualDesktopId(UUID.fromString(JsonParser.parseString(Files.readString(file)).asJsonObject.get("desktop").asString))
        Files.delete(file)
        val window = ProjectWindow.of(project) ?: return
        val moved = VirtualDesktops.move(window.handle, desktop)
        if (moved) windowsPutBack.add(mapOf("project" to window.project.toString(), "desktop" to desktop.value.toString()))
        thisLogger().info("The window of ${project.name} ${if (moved) "went back to" else "couldn't go back to, as it no longer exists,"} the virtual desktop ${desktop.value}.")
    }

    private fun fileOf(project: Project): Path = folder.resolve("${project.locationHash}.json")
}
