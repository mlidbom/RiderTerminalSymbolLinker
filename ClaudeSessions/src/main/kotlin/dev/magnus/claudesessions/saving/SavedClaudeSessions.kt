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
import dev.magnus.claudesessions.sessionhosts.SessionHostId
import java.nio.file.Files
import java.nio.file.Path
import java.nio.file.StandardCopyOption

/**
 * The tabs of a project's [ClaudeSessionsView], each with the Claude session that ran in it and its session host, in
 * their order, and the session whose tab was selected, as the [ClaudeSessionKeeper] last saved them: a file in the IDE's
 * system folder, which survives a restart. The view brings them back when it opens, and selects that tab again.
 */
class SavedClaudeSessions(project: Project) {
    private val file: Path = Path.of(PathManager.getSystemPath(), "claude-sessions", "${project.locationHash}.json")

    /** The saved tabs, or none when none were ever saved. */
    fun read(): ClaudeSessionsInTheView {
        if (!Files.exists(file)) return ClaudeSessionsInTheView.NONE
        val json = JsonParser.parseString(Files.readString(file)).asJsonObject
        //todo: the builds before session hosts (2026-11-19) saved the tabs as "sessions", each a session: drop the fallback
        //   once every IDE of Magnus's has saved with a later build.
        val tabs = json.getAsJsonArray("tabs") ?: json.getAsJsonArray("sessions")
        return ClaudeSessionsInTheView(
            tabs.map { it.asJsonObject.toTab() },
            json.get("selected")?.takeUnless { it.isJsonNull }?.asString?.let(::ClaudeSessionId),
        )
    }

    /** Replaces the saved tabs with [view]'s, at once: a reader never sees half of them. */
    fun write(view: ClaudeSessionsInTheView) {
        Files.createDirectories(file.parent)
        val written = file.resolveSibling("${file.fileName}.writing")
        val json = JsonObject().apply {
            add("tabs", JsonArray().apply { view.tabs.forEach { add(it.toJson()) } })
            addProperty("selected", view.selected?.value)
        }
        Files.writeString(written, gson.toJson(json))
        Files.move(written, file, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
    }

    /** Where the sessions are saved, for the development observation channel to show. */
    val location: Path get() = file

    /** [SavedTab] as JSON: its session's `id`, `workingDirectory` and `name`, when it has one, and its `sessionHost`. */
    private fun SavedTab.toJson(): JsonObject =
        JsonObject().apply {
            session?.let { session ->
                addProperty("id", session.id.value)
                addProperty("workingDirectory", session.workingDirectory.toString())
                addProperty("name", session.name)
            }
            addProperty("sessionHost", sessionHost?.value)
        }

    private fun JsonObject.toTab(): SavedTab =
        SavedTab(
            get("id")?.let { id -> ClaudeSession(ClaudeSessionId(id.asString), Path.of(get("workingDirectory").asString), get("name")?.takeUnless { it.isJsonNull }?.asString) },
            get("sessionHost")?.takeUnless { it.isJsonNull }?.asString?.let(::SessionHostId),
        )

    private companion object {
        val gson = GsonBuilder().setPrettyPrinting().disableHtmlEscaping().create()!!
    }
}
