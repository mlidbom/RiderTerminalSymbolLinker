package dev.magnus.claudesessions.hub

import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.claudecode.ClaudeSessionId
import dev.magnus.claudesessions.claudecode.RunningClaudeCode
import java.nio.file.Path

/**
 * A running session as the hub's [RunningSessionsAnswer] tells of it, in the answer's own terms: the process of its
 * Claude Code, which is the same across a `/clear`, the session that Claude Code runs now, its name, if it has one, and
 * the directory Claude Code was started in.
 */
data class RunningSessionInTheAnswer(val processId: Long, val sessionId: String, val name: String?, val directory: String) {
    /** The Claude Code it tells of, or null once its process no longer runs. */
    fun asRunningClaudeCode(): RunningClaudeCode? =
        ProcessHandle.of(processId).orElse(null)?.let { RunningClaudeCode(it, ClaudeSession(ClaudeSessionId(sessionId), Path.of(directory), name)) }
}
