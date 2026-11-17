package dev.magnus.claudesessions

import com.google.gson.JsonObject
import com.google.gson.JsonParseException
import com.google.gson.JsonParser
import java.nio.file.Files
import java.nio.file.NoSuchFileException
import java.nio.file.Path
import java.time.Instant

/**
 * A Claude Code session: a conversation with Claude that `claude --resume <id>` continues, in the folder it ran in.
 *
 * @property id The session's id, which Claude Code names the session's conversation file after.
 * @property workingDirectory The folder Claude Code ran in. Claude Code keeps each folder's sessions apart, so a session
 * is resumed in its folder.
 * @property name The name Claude Code shows for the session, if it has one: one the user gave it, or one Claude Code
 * derived.
 */
data class ClaudeSession(val id: ClaudeSessionId, val workingDirectory: Path, val name: String?) {
    companion object {
        /**
         * The session Claude Code runs in [process], or in a process below it, or null when it runs none.
         *
         * Below it, because `claude` may be a script that starts Claude Code, as an npm install's `claude.cmd` does.
         */
        fun runningIn(process: ProcessHandle): ClaudeSession? =
            (sequenceOf(process) + process.descendants().iterator().asSequence()).firstNotNullOfOrNull(ClaudeCodeSessionsFolder::sessionOf)
    }
}

/** The id of a [ClaudeSession]: a UUID, as Claude Code writes it. */
@JvmInline
value class ClaudeSessionId(val value: String)

/**
 * The folder where Claude Code records each session that runs, as a file named after the process that runs it:
 * `sessions/<process id>.json` in Claude Code's own folder. Claude Code writes the file when it starts, and deletes it
 * when it exits. A Claude Code that is ended, as when its tab closes, leaves its record behind, for a later process
 * with the same id to find: a record written before the process started is another's.
 *
 * Claude Code's own folder is the one the environment variable `CLAUDE_CONFIG_DIR` names, as in Claude Code, or
 * `.claude` in the user's home folder.
 */
object ClaudeCodeSessionsFolder {
    private val folder: Path
        get() = (System.getenv("CLAUDE_CONFIG_DIR")?.let { Path.of(it) } ?: Path.of(System.getProperty("user.home"), ".claude"))
            .resolve("sessions")

    /**
     * The session that [process] runs, or null when there is no whole record of one to read: when the process isn't
     * Claude Code, when Claude Code hasn't written the record yet, or has just deleted it as it exits, and while it
     * rewrites it. Or when the record is that of an earlier process with the same id.
     */
    fun sessionOf(process: ProcessHandle): ClaudeSession? {
        val record = folder.resolve("${process.pid()}.json")
        if (!Files.exists(record)) return null
        // Claude Code deletes the record as it exits, and rewrites it as the session changes, while this reads it: Claude
        // Code's own reader of these records copes with one torn by a write, as this does by returning null.
        val text = try {
            Files.readString(record)
        } catch (_: NoSuchFileException) {
            return null
        }
        val json = try {
            JsonParser.parseString(text).asJsonObject
        } catch (_: JsonParseException) {
            return null
        }
        return if (wasWrittenBefore(process, json)) null else sessionIn(json)
    }

    /** Whether Claude Code wrote [record] before [process] started, as the earlier process with its id did. */
    private fun wasWrittenBefore(process: ProcessHandle, record: JsonObject): Boolean {
        val processStarted = process.info().startInstant().orElse(null) ?: return false
        val recordWritten = record.get("startedAt")?.takeUnless { it.isJsonNull }?.asLong?.let(Instant::ofEpochMilli) ?: return false
        return recordWritten.isBefore(processStarted.minusSeconds(1))
    }

    private fun sessionIn(record: JsonObject): ClaudeSession =
        ClaudeSession(
            ClaudeSessionId(record.get("sessionId").asString),
            Path.of(record.get("cwd").asString),
            record.get("name")?.takeUnless { it.isJsonNull }?.asString,
        )
}
