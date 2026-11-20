package dev.magnus.claudesessions.claudecode

import dev.magnus.claudesessions.sessionhosts.SessionHost
import dev.magnus.claudesessions.sessionhosts.SessionHostId

/**
 * A Claude Code that runs: its process, the [session] it runs now, what it's doing, the [SessionHost] whose console it
 * runs in, if it runs in one, as Claude Code started in a tab of the view does, what it last showed under its input box,
 * and its turn under way, if one is. See [RunningClaudeCodes].
 */
data class RunningClaudeCode(
    val process: ProcessHandle,
    val session: ClaudeSession,
    val status: Status,
    val sessionHost: SessionHostId?,
    /** Null until it has shown its status line to the hub: within a minute of a hub's start, as after a publish. */
    val statusLine: ClaudeCodesStatusLine?,
    /** Null while no turn is under way, and in a session that doesn't load the `tooling-commands` mod. */
    val turnUnderWay: TurnUnderWay?,
) {
    /** What a Claude Code that runs is doing, as the record it keeps of its session says, in its `status`. */
    enum class Status {
        /** It waits for the next prompt. */
        IDLE,

        /** It waits for the next prompt, with a background shell task or monitor running. */
        IDLE_WITH_A_BACKGROUND_TASK,

        /** It works: a turn, a compaction, or a background agent. A message sent now is queued until it's done. */
        BUSY,

        /**
         * It waits for the user in a dialog: a permission prompt, a question, or a picker a slash command opened. Only its
         * own UI draws those.
         */
        WAITING,

        /** A status this build doesn't know, which a later Claude Code reports. */
        OTHER,
    }
}
