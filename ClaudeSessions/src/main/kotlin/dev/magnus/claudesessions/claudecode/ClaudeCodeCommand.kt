package dev.magnus.claudesessions.claudecode

import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.hub.ClaudeToolingHub

/**
 * What a [ClaudeSessionTab]'s shell runs to run Claude Code: `claude`, `claude --name <name>`, or
 * `claude --resume <id>`, as a user types it, and the shell finds on its `PATH`. The [ClaudeToolingHub] types it into the
 * shell, quoted as that shell reads it, as it owns all typing into Claude Code and the shell under it.
 *
 * Typed into the shell, Claude Code runs as the shell's own child, as in any terminal tab. So tooling that reloads a
 * session the way a user would works in the tab: it types `/exit` into Claude Code, and then `claude --resume <id>` into
 * the shell, which Claude Code's exit leaves at its prompt.
 */
sealed interface ClaudeCodeCommand {
    /** `claude`, which starts a new session, or `claude --name <name>`, which starts one named [name], unless that's null. */
    data class StartingANewSession(val name: String?) : ClaudeCodeCommand

    /** `claude --resume <id>`, which resumes [session]. */
    data class Resuming(val session: ClaudeSession) : ClaudeCodeCommand
}
