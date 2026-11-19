package dev.magnus.claudesessions.claudecode

import java.nio.file.Path

/**
 * A Claude Code session: a conversation with Claude that `claude --resume <id>` continues, in the folder it ran in.
 *
 * @property id The session's id, which Claude Code names the session's transcript after.
 * @property workingDirectory The folder Claude Code ran in. Claude Code keeps each folder's sessions apart, so a session
 * is resumed in its folder.
 * @property name The name Claude Code shows for the session, if it has one: one the user gave it, or one Claude Code
 * derived.
 */
data class ClaudeSession(val id: ClaudeSessionId, val workingDirectory: Path, val name: String?) {
    /**
     * Whether `claude --resume` can continue the session: Claude Code still has its transcript. It hasn't when the
     * session was archived or deleted since it ran.
     */
    val canBeResumed: Boolean get() = ClaudeCodeTranscripts.hasTheTranscriptOf(id)
}
