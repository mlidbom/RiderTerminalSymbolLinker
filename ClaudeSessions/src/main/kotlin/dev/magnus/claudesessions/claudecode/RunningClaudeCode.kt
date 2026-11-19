package dev.magnus.claudesessions.claudecode

import dev.magnus.claudesessions.sessionhosts.SessionHost
import dev.magnus.claudesessions.sessionhosts.SessionHostId

/**
 * A Claude Code that runs: its process, the [session] it runs now, what it's doing, and the [SessionHost] whose console
 * it runs in, if it runs in one, as Claude Code started in a tab of the view does. See [RunningClaudeCodes].
 */
data class RunningClaudeCode(val process: ProcessHandle, val session: ClaudeSession, val status: Status, val sessionHost: SessionHostId?) {
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
