package dev.magnus.claudesessions.claudecode

/** A Claude Code that runs: its process, and the [session] it runs now. See [RunningClaudeCodes]. */
data class RunningClaudeCode(val process: ProcessHandle, val session: ClaudeSession)
