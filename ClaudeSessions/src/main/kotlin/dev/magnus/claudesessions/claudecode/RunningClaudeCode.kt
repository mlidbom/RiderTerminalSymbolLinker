package dev.magnus.claudesessions.claudecode

/** A Claude Code that runs: its process, and the [session] it runs now. */
data class RunningClaudeCode(val process: ProcessHandle, val session: ClaudeSession) {
    companion object {
        /**
         * The Claude Code that runs as [process] or below it, or null when none does: a tab's process is its shell, and
         * Claude Code runs below it.
         */
        fun inOrBelow(process: ProcessHandle): RunningClaudeCode? =
            (sequenceOf(process) + process.descendants().iterator().asSequence()).firstNotNullOfOrNull { candidate ->
                ClaudeCodeSessionsFolder.sessionOf(candidate)?.let { RunningClaudeCode(candidate, it) }
            }
    }
}
