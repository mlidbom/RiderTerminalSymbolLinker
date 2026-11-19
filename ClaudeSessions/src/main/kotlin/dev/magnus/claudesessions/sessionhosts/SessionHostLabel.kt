package dev.magnus.claudesessions.sessionhosts

import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.google.gson.JsonSyntaxException
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.project.Project
import dev.magnus.claudesessions.ClaudeSessionsView
import java.nio.file.Path

/**
 * What the plugin gives the hub to know each [SessionHost] it starts by, as the host's `label`: the IDE, by its config
 * folder, and the project whose [ClaudeSessionsView] the host's tab is in. So, at the IDE's start, the view finds the
 * hosts it started and didn't save, as when the IDE crashed within two seconds of a new tab, before its keeper looked.
 *
 * The hub keeps a label as it's given, and other tools may start hosts with labels of their own.
 */
data class SessionHostLabel(val ideConfigFolder: Path, val project: Path) {
    /** The label as the hub keeps it: `{"ideConfigFolder": …, "project": …}`. */
    val text: String
        get() = JsonObject().apply {
            addProperty(IDE_CONFIG_FOLDER, ideConfigFolder.toString())
            addProperty(PROJECT, project.toString())
        }.toString()

    companion object {
        private const val IDE_CONFIG_FOLDER = "ideConfigFolder"
        private const val PROJECT = "project"

        /** The label of the hosts of [project]'s view, in this IDE. */
        fun ofTheViewOf(project: Project): SessionHostLabel = SessionHostLabel(Path.of(PathManager.getConfigPath()), Path.of(project.basePath!!))

        /** The label [text] says, or null for a label this plugin didn't give, as another tool's, or none. */
        fun of(text: String?): SessionHostLabel? {
            val json = try {
                text?.let(JsonParser::parseString)?.takeIf { it.isJsonObject }?.asJsonObject
            } catch (_: JsonSyntaxException) {
                null
            } ?: return null
            val ideConfigFolder = json.get(IDE_CONFIG_FOLDER)?.takeIf { it.isJsonPrimitive }?.asString ?: return null
            val project = json.get(PROJECT)?.takeIf { it.isJsonPrimitive }?.asString ?: return null
            return SessionHostLabel(Path.of(ideConfigFolder), Path.of(project))
        }
    }
}
