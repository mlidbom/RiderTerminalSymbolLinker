package dev.magnus.claudesessions.claudecode

import com.google.gson.JsonObject
import com.google.gson.JsonParseException
import com.google.gson.JsonParser
import java.nio.file.Files
import java.nio.file.NoSuchFileException
import java.nio.file.Path
import java.time.Instant

/**
 * The folder where Claude Code records each session that runs, as a file named after the process that runs it:
 * `sessions/<process id>.json` in [ClaudeCodeFolder]. Claude Code writes the file when it starts, and deletes it when it
 * exits. A Claude Code that is ended, as when its tab closes, leaves its record behind, for a later process with the
 * same id to find: a record written before the process started is another's.
 */
object ClaudeCodeSessionsFolder {
    private val folder: Path get() = ClaudeCodeFolder.path.resolve("sessions")

    /** Every Claude Code that runs now, wherever it runs, as its record tells. */
    fun running(): List<RunningClaudeCode> {
        if (!Files.isDirectory(folder)) return emptyList()
        val processIds = Files.newDirectoryStream(folder, "*.json").use { records -> records.mapNotNull { it.fileName.toString().removeSuffix(".json").toLongOrNull() } }
        return processIds.mapNotNull { ProcessHandle.of(it).orElse(null) }.mapNotNull { process -> sessionOf(process)?.let { RunningClaudeCode(process, it) } }
    }

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
