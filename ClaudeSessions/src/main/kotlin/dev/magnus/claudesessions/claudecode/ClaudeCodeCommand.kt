package dev.magnus.claudesessions.claudecode

import dev.magnus.claudesessions.ClaudeSessionTab

/**
 * What a [ClaudeSessionTab] types into its shell to run Claude Code: the `claude` command, as a user types it, which the
 * shell finds on its `PATH`, with its arguments. The tab types it as its [Shell] reads it.
 *
 * Typed into the shell, Claude Code runs as the shell's own child, as in any terminal tab. So tooling that reloads a
 * session the way a user would works in the tab: it types `/exit` into Claude Code, and then `claude --resume <id>` into
 * the shell, which Claude Code's exit leaves at its prompt.
 *
 * @property words `claude`, and its arguments, each as Claude Code is to get it.
 */
class ClaudeCodeCommand private constructor(val words: List<String>) {
    companion object {
        /** The command that starts a new session: named [name], with `--name`, unless that's null. */
        fun startingANewSession(name: String?): ClaudeCodeCommand =
            ClaudeCodeCommand(listOf("claude") + (name?.let { listOf("--name", it) } ?: emptyList()))

        /** The command that resumes [session]. */
        fun resuming(session: ClaudeSession): ClaudeCodeCommand = ClaudeCodeCommand(listOf("claude", "--resume", session.id.value))
    }
}
