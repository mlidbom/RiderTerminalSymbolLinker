package dev.magnus.claudesessions.claudecode

import java.nio.file.Path

/**
 * Claude Code's own folder: the one the environment variable `CLAUDE_CONFIG_DIR` names, as in Claude Code, or `.claude`
 * in the user's home folder.
 */
object ClaudeCodeFolder {
    val path: Path
        get() = System.getenv("CLAUDE_CONFIG_DIR")?.let { Path.of(it) } ?: Path.of(System.getProperty("user.home"), ".claude")
}
