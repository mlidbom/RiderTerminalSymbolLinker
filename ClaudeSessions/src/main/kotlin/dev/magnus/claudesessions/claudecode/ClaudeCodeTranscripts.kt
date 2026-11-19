package dev.magnus.claudesessions.claudecode

import java.nio.file.Files
import java.nio.file.Path

/**
 * The transcripts Claude Code keeps of its sessions, which `claude --resume` continues: each session's as
 * `projects/<a name made from its folder>/<session id>.jsonl` in [ClaudeCodeFolder]. Archiving or deleting a session
 * moves or removes its transcript.
 */
object ClaudeCodeTranscripts {
    private val folder: Path get() = ClaudeCodeFolder.path.resolve("projects")

    /** Whether Claude Code has a transcript of the session [id], in the folder of any project. */
    fun hasTheTranscriptOf(id: ClaudeSessionId): Boolean {
        if (!Files.isDirectory(folder)) return false
        return Files.newDirectoryStream(folder).use { projects -> projects.any { Files.isRegularFile(it.resolve("${id.value}.jsonl")) } }
    }

    /** Every transcript Claude Code has, of every project's sessions: not those of the agents a session ran. */
    fun all(): List<ClaudeCodeTranscript> {
        if (!Files.isDirectory(folder)) return emptyList()
        val projects = Files.newDirectoryStream(folder) { Files.isDirectory(it) }.use { it.toList() }
        return projects.flatMap { project -> Files.newDirectoryStream(project, "*.jsonl").use { transcripts -> transcripts.map(::ClaudeCodeTranscript) } }
    }
}
