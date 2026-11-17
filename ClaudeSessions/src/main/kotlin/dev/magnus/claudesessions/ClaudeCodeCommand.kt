package dev.magnus.claudesessions

/**
 * What a [ClaudeSessionTab] types into its shell to run Claude Code: the `claude` command, as a user types it, which the
 * shell finds on its `PATH`.
 *
 * Typed into the shell, Claude Code runs as the shell's own child, as in any terminal tab. So tooling that reloads a
 * session the way a user would works in the tab: it types `/exit` into Claude Code, and then `claude --resume <id>` into
 * the shell, which Claude Code's exit leaves at its prompt.
 */
object ClaudeCodeCommand {
    /** The command that starts a new session. */
    fun startingANewSession(): String = "claude"

    /** The command that resumes [session]. */
    fun resuming(session: ClaudeSession): String = "claude --resume ${session.id.value}"
}
