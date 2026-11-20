package dev.magnus.claudesessions.claudecode

import java.time.Instant

/**
 * What a running Claude Code's turn under way does, as its own UI's spinner line tells it, such as "✻ Thinking… (12s ·
 * ↓ 1.2k tokens)", before any of it is in the transcript. The Claude tooling's hub keeps it, from what the
 * `tooling-commands` mod of each Claude Code session reports of the session's main loop: a subagent's turns are left
 * out.
 *
 * The chat shows it over its input box. Magnus, 2026-11-20: "The lack of any information about that in the new UI is
 * what keeps me switching back to the TUI constantly".
 */
data class TurnUnderWay(
    /** When the turn started: what its time taken so far is counted from. */
    val startedAt: Instant,
    val activity: Activity,
    /** While the model writes a tool call's input, the call's tool, such as `Write`: null otherwise. */
    val toolBeingWritten: String?,
    /** The turn's output tokens so far, as the spinner's "↓ 1.2k tokens" counts them. */
    val outputTokensSoFar: Long,
) {
    /** What the turn does now. */
    enum class Activity {
        /** Claude Code has sent the model a request, and no response streams yet. */
        REQUESTING,

        THINKING,

        /** The model writes text. */
        WRITING,

        /** The model writes a tool call's input, as while it writes a large file. */
        WRITING_A_TOOL_CALL,

        /** The model asked for tools, which run, or wait for the user to allow one. */
        RUNNING_TOOLS,

        /** What a later hub tells of, which this build doesn't know. */
        OTHER,
    }
}
