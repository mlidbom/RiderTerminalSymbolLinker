package dev.magnus.claudesessions.chat.saving

import com.google.gson.GsonBuilder
import com.google.gson.JsonArray
import com.google.gson.JsonElement
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.google.gson.JsonPrimitive
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.project.Project
import dev.magnus.claudesessions.chat.MessageMaybeSent
import dev.magnus.claudesessions.chat.TabCard
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.MessagePart
import dev.magnus.claudesessions.chat.claudecode.PastedImage
import dev.magnus.claudesessions.chat.claudecode.PastedText
import dev.magnus.claudesessions.chat.claudecode.TypedText
import dev.magnus.claudesessions.chat.messagebox.Draft
import dev.magnus.claudesessions.chat.transcript.TranscriptPosition
import dev.magnus.claudesessions.sessionhosts.SessionHostId
import java.nio.file.Files
import java.nio.file.Path
import java.nio.file.StandardCopyOption

/**
 * The chats of a project's tabs, as their [ChatKeeper]s saved them: a file for each, in the IDE's system folder, which
 * survives a restart, named by the session host the tab's session runs in. A tab that comes back attached to its host
 * finds its chat by the host; one whose session is resumed in a new host, as after a reboot, by the host it had.
 */
class SavedChats(project: Project) {
    private val folder: Path = Path.of(PathManager.getSystemPath(), "claude-sessions", "chats", project.locationHash)

    /** The chat saved for the tab whose session ran in [host], or null when none was. */
    fun read(host: SessionHostId): SavedChat? {
        val file = fileOf(host)
        if (!Files.exists(file)) return null
        return JsonParser.parseString(Files.readString(file)).asJsonObject.toChat()
    }

    /** Saves [chat] as the chat of the tab whose session runs in [host], at once: a reader never sees half of it. */
    fun write(host: SessionHostId, chat: SavedChat) {
        Files.createDirectories(folder)
        val file = fileOf(host)
        val written = file.resolveSibling("${file.fileName}.writing")
        Files.writeString(written, gson.toJson(chat.toJson()))
        Files.move(written, file, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
    }

    /** Forgets the chat of the tab whose session ran in [host], as the user closed the tab. */
    fun delete(host: SessionHostId) {
        Files.deleteIfExists(fileOf(host))
    }

    /** Forgets the chats of every tab but those whose sessions run in [kept]: tabs that didn't come back. */
    fun deleteAllBut(kept: Set<SessionHostId>) {
        if (!Files.isDirectory(folder)) return
        val keptFiles = kept.map(::fileOf).toSet()
        Files.newDirectoryStream(folder, "*.json").use { files -> files.filter { it !in keptFiles }.forEach(Files::deleteIfExists) }
    }

    private fun fileOf(host: SessionHostId): Path {
        check(host.value.matches(A_FILE_NAME)) { "The session host's id ${host.value} can't be a file's name." }
        return folder.resolve("${host.value}.json")
    }

    private fun SavedChat.toJson(): JsonObject = JsonObject().apply {
        add("draft", draft.message.toJson().apply { addProperty("caret", draft.caret) })
        add("notSent", notSent.map { it.toJson() }.toJsonArray())
        add("maybeSent", maybeSent.map { JsonObject().apply { add("message", it.message.toJson()); addProperty("lastItemBefore", it.lastItemBefore) } }.toJsonArray())
        add("transcript", JsonObject().apply {
            addProperty("topRow", transcript.topRow)
            addProperty("pixelsIntoIt", transcript.pixelsIntoIt)
            add("opened", transcript.opened.map(::JsonPrimitive).toJsonArray())
        })
        addProperty("cardTheUserChose", cardTheUserChose?.name)
    }

    private fun JsonObject.toChat(): SavedChat {
        val draft = getAsJsonObject("draft")
        val transcript = getAsJsonObject("transcript")
        return SavedChat(
            Draft(draft.toMessage(), draft.get("caret").asInt),
            getAsJsonArray("notSent").map { it.asJsonObject.toMessage() },
            getAsJsonArray("maybeSent").map { it.asJsonObject.let { maybeSent -> MessageMaybeSent(maybeSent.getAsJsonObject("message").toMessage(), maybeSent.stringOrNull("lastItemBefore")) } },
            transcript.toTranscriptPosition(),
            stringOrNull("cardTheUserChose")?.let(TabCard::valueOf),
        )
    }

    /**
     * The [TranscriptPosition] this JSON saved: as a chat saves it, or as a chat of the transcript in an editor saved
     * it, before 0.1.0 rendered the conversation's markdown, with the item at the top in place of the row, lines into
     * it in place of pixels, and the open tool calls as its folds: the row of an item that isn't a tool call has the
     * item's id as its key, as a tool call's row has the call's.
     */
    private fun JsonObject.toTranscriptPosition(): TranscriptPosition {
        //todo: Drop the names of the transcript in an editor once every IDE has saved its chats anew (Claude, 2026-11-20).
        val isOfAnEditor = has("linesIntoIt")
        return TranscriptPosition(
            stringOrNull(if (isOfAnEditor) "topItem" else "topRow"),
            if (isOfAnEditor) 0 else get("pixelsIntoIt").asInt,
            getAsJsonArray(if (isOfAnEditor) "openFolds" else "opened").map { it.asString }.toSet(),
        )
    }

    /** [Message] as JSON: its `parts`, each with its `kind`, `typed`, `pasted` or `image`, its `text`, and an image's `file`. */
    private fun Message.toJson(): JsonObject = JsonObject().apply {
        add("parts", parts.map { part ->
            JsonObject().apply {
                addProperty("kind", part.kind)
                addProperty("text", part.text)
                if (part is PastedImage) addProperty("file", part.file.toString())
            }
        }.toJsonArray())
    }

    /** The message this JSON is: an image whose file is gone, deleted a week after it was pasted, comes back as its text. */
    private fun JsonObject.toMessage(): Message = Message(getAsJsonArray("parts").map { it.asJsonObject.toPart() })

    private fun JsonObject.toPart(): MessagePart {
        val text = get("text").asString
        return when (val kind = get("kind").asString) {
            "typed" -> TypedText(text)
            "pasted" -> PastedText(text)
            "image" -> Path.of(get("file").asString).let { file -> if (Files.isRegularFile(file)) PastedImage(file, text) else TypedText(text) }
            else -> error("No part of a message of the kind $kind")
        }
    }

    private val MessagePart.kind: String
        get() = when (this) {
            is TypedText -> "typed"
            is PastedText -> "pasted"
            is PastedImage -> "image"
        }

    private fun JsonObject.stringOrNull(name: String): String? = get(name)?.takeUnless(JsonElement::isJsonNull)?.asString

    private fun List<JsonElement>.toJsonArray(): JsonArray = JsonArray().also { array -> forEach(array::add) }

    private companion object {
        val gson = GsonBuilder().setPrettyPrinting().disableHtmlEscaping().serializeNulls().create()!!

        /** What a session host's id may be, to name its tab's file: the hub's ids are hexadecimal. */
        val A_FILE_NAME = Regex("[A-Za-z0-9_-]+")
    }
}
