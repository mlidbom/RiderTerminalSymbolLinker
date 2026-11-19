package dev.magnus.claudesessions.saving

import com.google.gson.GsonBuilder
import com.google.gson.JsonArray
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.project.Project
import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.claudecode.ClaudeSessionId
import java.nio.file.Files
import java.nio.file.Path
import java.nio.file.StandardCopyOption

/**
 * The Claude sessions that ran in a project's [ClaudeSessionsView], in the order of its tabs, and the one whose tab was
 * selected, as the [ClaudeSessionKeeper] last saved them: a file in the IDE's system folder, which survives a restart.
 * The view resumes them when it opens, and selects that tab again.
 */
class SavedClaudeSessions(project: Project) {
    private val file: Path = Path.of(PathManager.getSystemPath(), "claude-sessions", "${project.locationHash}.json")

    /** The saved sessions, or none when none were ever saved. */
    fun read(): ClaudeSessionsInTheView {
        if (!Files.exists(file)) return ClaudeSessionsInTheView.NONE
        val json = JsonParser.parseString(Files.readString(file)).asJsonObject
        return ClaudeSessionsInTheView(
            json.getAsJsonArray("sessions").map { it.asJsonObject.toSession() },
            json.get("selected")?.takeUnless { it.isJsonNull }?.asString?.let(::ClaudeSessionId),
        )
    }

    /** Replaces the saved sessions with [sessions], at once: a reader never sees half of them. */
    fun write(sessions: ClaudeSessionsInTheView) {
        Files.createDirectories(file.parent)
        val written = file.resolveSibling("${file.fileName}.writing")
        val json = JsonObject().apply {
            add("sessions", JsonArray().apply { sessions.sessions.forEach { add(it.toJson()) } })
            addProperty("selected", sessions.selected?.value)
        }
        Files.writeString(written, gson.toJson(json))
        Files.move(written, file, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
    }

    /** Where the sessions are saved, for the development observation channel to show. */
    val location: Path get() = file

    private fun ClaudeSession.toJson(): JsonObject =
        JsonObject().apply {
            addProperty("id", id.value)
            addProperty("workingDirectory", workingDirectory.toString())
            addProperty("name", name)
        }

    private fun JsonObject.toSession(): ClaudeSession =
        ClaudeSession(
            ClaudeSessionId(get("id").asString),
            Path.of(get("workingDirectory").asString),
            get("name")?.takeUnless { it.isJsonNull }?.asString,
        )

    private companion object {
        val gson = GsonBuilder().setPrettyPrinting().disableHtmlEscaping().create()!!
    }
}
