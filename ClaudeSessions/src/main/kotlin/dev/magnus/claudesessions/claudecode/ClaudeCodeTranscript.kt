package dev.magnus.claudesessions.claudecode

import com.google.gson.stream.JsonReader
import com.google.gson.stream.JsonToken
import com.google.gson.stream.MalformedJsonException
import java.io.EOFException
import java.io.StringReader
import java.nio.ByteBuffer
import java.nio.channels.FileChannel
import java.nio.file.Files
import java.nio.file.Path
import java.time.Instant

/**
 * The transcript Claude Code keeps of a session, which `claude --resume` continues: a file in [ClaudeCodeTranscripts]
 * named after the session, with a line of JSON for each entry, appended as the session goes on. A session resumed from
 * another folder goes on in the same file, and its entries name the folder they were written in.
 *
 * Claude Code writes the session's names and its last prompt again and again as the session goes on, so the end of the
 * file tells what the session is now: [summary] reads only that, as the whole of a long session's transcript runs to
 * hundreds of megabytes.
 */
class ClaudeCodeTranscript(private val file: Path) {
    /** The session whose transcript this is. */
    val sessionId: ClaudeSessionId get() = ClaudeSessionId(file.fileName.toString().removeSuffix(".jsonl"))

    /** When Claude Code last wrote to the transcript: when the session was last active. */
    val lastWritten: Instant get() = Files.getLastModifiedTime(file).toInstant()

    /**
     * What the transcript's last entries tell of the session, or null when it has no conversation to resume: no prompt
     * was ever typed into it. A session `claude -p` ran, a tool's single prompt rather than a conversation, is told by
     * its [Summary.wasRunByATool].
     */
    fun summary(): Summary? {
        val size = Files.size(file)
        val fromTheEnd = if (size > END_TO_READ) fieldsIn(linesOfTheEnd(size - END_TO_READ)) else null
        val fields = fromTheEnd?.takeIf { it.folder != null } ?: Files.newBufferedReader(file).useLines(::fieldsIn)
        val folder = fields.folder ?: return null
        return Summary(
            ClaudeSession(sessionId, Path.of(folder), fields.customTitle ?: fields.aiTitle),
            fields.lastPrompt,
            fields.gitBranch,
            lastWritten,
            fields.entrypoint?.startsWith("sdk") == true,
        )
    }

    /**
     * What a [ClaudeCodeTranscript] tells of its session.
     *
     * @property session The session, in the folder it last ran in, named as the user named it with `/rename`, or else
     * with the title Claude Code gave it.
     * @property lastPrompt The last prompt typed into it.
     * @property gitBranch The git branch its folder had checked out as it last ran, when it was in git.
     * @property lastActive When Claude Code last wrote to the transcript.
     * @property wasRunByATool Whether `claude -p` ran it, as a tool runs a single prompt, rather than a user a
     * conversation: Claude Code names it an SDK's session.
     */
    data class Summary(val session: ClaudeSession, val lastPrompt: String?, val gitBranch: String?, val lastActive: Instant, val wasRunByATool: Boolean)

    /** The whole lines of the file after [start]. */
    private fun linesOfTheEnd(start: Long): Sequence<String> {
        val bytes = FileChannel.open(file).use { channel ->
            val buffer = ByteBuffer.allocate((channel.size() - start).toInt())
            channel.position(start)
            do {
                val read = channel.read(buffer)
            } while (read >= 0 && buffer.hasRemaining())
            buffer.array().copyOf(buffer.position())
        }
        val firstWholeLine = bytes.indexOf('\n'.code.toByte()) + 1
        return String(bytes, firstWholeLine, bytes.size - firstWholeLine, Charsets.UTF_8).lineSequence()
    }

    /** The fields Claude Sessions reads, as the last of [lines] that have each give them. */
    private fun fieldsIn(lines: Sequence<String>): Fields {
        val fields = Fields()
        for (line in lines) {
            if (line.isBlank()) continue
            val entry = topLevelTextFieldsOf(line) ?: continue
            when (entry["type"]) {
                "custom-title" -> entry["customTitle"]?.let { fields.customTitle = it }
                "ai-title" -> entry["aiTitle"]?.let { fields.aiTitle = it }
                "last-prompt" -> entry["lastPrompt"]?.let { fields.lastPrompt = it }
                "user", "assistant" -> if (entry["isSidechain"] != "true") entry["cwd"]?.let {
                    fields.folder = it
                    fields.gitBranch = entry["gitBranch"]
                    fields.entrypoint = entry["entrypoint"]
                }
            }
        }
        return fields
    }

    /**
     * The text and boolean fields at the top of [line]'s entry, as text: read as a stream, skipping the rest, as an
     * entry can hold a whole request to the model. Null for a line Claude Code is still writing as this reads it: the
     * last line of a session that runs may be cut off.
     */
    private fun topLevelTextFieldsOf(line: String): Map<String, String>? = try {
        JsonReader(StringReader(line)).use { reader ->
            val fields = HashMap<String, String>()
            reader.beginObject()
            while (reader.hasNext()) {
                val name = reader.nextName()
                when {
                    name !in FIELDS_READ -> reader.skipValue()
                    reader.peek() == JsonToken.STRING -> fields[name] = reader.nextString()
                    reader.peek() == JsonToken.BOOLEAN -> fields[name] = reader.nextBoolean().toString()
                    else -> reader.skipValue()
                }
            }
            fields
        }
    } catch (_: EOFException) {
        null
    } catch (_: MalformedJsonException) {
        null
    }

    private class Fields {
        var customTitle: String? = null
        var aiTitle: String? = null
        var lastPrompt: String? = null
        var folder: String? = null
        var gitBranch: String? = null
        var entrypoint: String? = null
    }

    private companion object {
        /**
         * How much of the end of the file [summary] reads: in Magnus's 67 transcripts of 2026-11-18, the last of each
         * field it reads was at most 208 KB from the end. A transcript whose end names no folder is read whole.
         */
        const val END_TO_READ = 1024L * 1024

        val FIELDS_READ = setOf("type", "customTitle", "aiTitle", "lastPrompt", "isSidechain", "cwd", "gitBranch", "entrypoint")
    }
}
