package dev.magnus.claudesessions.hub

import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.claudecode.ClaudeSessionId
import dev.magnus.claudesessions.claudecode.RunningClaudeCode
import dev.magnus.claudesessions.sessionhosts.SessionHostId
import java.nio.file.Path

/**
 * A running session as the hub's [RunningSessionsAnswer] tells of it, in the answer's own terms: the process of its
 * Claude Code, which is the same across a `/clear`, the session that Claude Code runs now, its status, as Claude Code
 * names it, its name, if it has one, the directory Claude Code was started in, and the session host whose console it
 * runs in, if it runs in one.
 */
data class RunningSessionInTheAnswer(val processId: Long, val sessionId: String, val status: String, val name: String?, val directory: String, val sessionHost: String?) {
    /** The Claude Code it tells of, or null once its process no longer runs. */
    fun asRunningClaudeCode(): RunningClaudeCode? =
        ProcessHandle.of(processId).orElse(null)?.let { RunningClaudeCode(it, ClaudeSession(ClaudeSessionId(sessionId), Path.of(directory), name), statusOfTheClaudeCode(), sessionHost?.let(::SessionHostId)) }

    /** The status of the Claude Code it tells of: one this build doesn't know is [RunningClaudeCode.Status.OTHER], as the contract has readers ignore it. */
    private fun statusOfTheClaudeCode(): RunningClaudeCode.Status = when (status) {
        "idle" -> RunningClaudeCode.Status.IDLE
        "shell" -> RunningClaudeCode.Status.IDLE_WITH_A_BACKGROUND_TASK
        "busy" -> RunningClaudeCode.Status.BUSY
        "waiting" -> RunningClaudeCode.Status.WAITING
        else -> RunningClaudeCode.Status.OTHER
    }
}
