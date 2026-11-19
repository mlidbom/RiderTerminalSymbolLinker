package dev.magnus.claudesessions.chat.claudecode

/**
 * Whether Claude Code runs in a tab, and what it is doing: as the record Claude Code keeps of each session that runs
 * says, in its `status`.
 */
enum class ClaudeCodeStatus {
    /** No Claude Code runs in the tab: it hasn't started yet, or it has exited, and the shell is at its prompt. */
    NOT_RUNNING,

    /** Claude Code waits for the next prompt. */
    IDLE,

    /** Claude Code works on a prompt: a message sent now is queued until it's done. */
    BUSY,

    /**
     * Claude Code waits for the user in a dialog: a permission prompt, a question, or a picker a slash command opened.
     * Only its own UI, in the terminal, draws those.
     */
    WAITING,
}
