package dev.magnus.claudesessions

import com.intellij.openapi.util.SystemInfo
import java.nio.file.Files
import java.nio.file.Path

/**
 * The command line a [ClaudeSessionTab] runs Claude Code with: the `claude` command as a shell would find it on the
 * `PATH`, or in `~/.local/bin`, where Claude Code's own installer puts it, which an IDE started from the desktop may
 * not have on its `PATH`.
 *
 * A tab runs Claude Code itself, not a shell, so this finds the command as the shell would have: on Windows with each
 * of the extensions `PATHEXT` lists, and a `.cmd` or `.bat` script, as an npm install's `claude.cmd` is, through
 * `cmd.exe`, as Windows can't start a script by itself.
 */
object ClaudeCodeCommand {
    /** The command line that starts a new session. */
    fun startingANewSession(): List<String> = claude()

    /** The command line that resumes [session]. */
    fun resuming(session: ClaudeSession): List<String> = claude() + listOf("--resume", session.id.value)

    private fun claude(): List<String> {
        val executable = foldersToLookIn().flatMap { folder -> fileNames().map(folder::resolve) }.firstOrNull(Files::isRegularFile)
            ?: throw ClaudeCodeNotFoundException(foldersToLookIn())
        val isAScript = executable.fileName.toString().lowercase().let { it.endsWith(".cmd") || it.endsWith(".bat") }
        return if (isAScript) listOf("cmd.exe", "/c", executable.toString()) else listOf(executable.toString())
    }

    private fun foldersToLookIn(): List<Path> =
        System.getenv("PATH").orEmpty().split(java.io.File.pathSeparatorChar).filter { it.isNotBlank() }.map { Path.of(it.trim('"')) } +
            Path.of(System.getProperty("user.home"), ".local", "bin")

    private fun fileNames(): List<String> =
        if (SystemInfo.isWindows) {
            (System.getenv("PATHEXT") ?: ".COM;.EXE;.BAT;.CMD").split(';').filter { it.isNotBlank() }.map { "claude" + it.lowercase() }
        } else {
            listOf("claude")
        }
}

/** Thrown when there is no `claude` command to run: Claude Code isn't installed, or not where [ClaudeCodeCommand] looks. */
class ClaudeCodeNotFoundException(foldersLookedIn: List<Path>) :
    RuntimeException("Found no 'claude' command in any of: ${foldersLookedIn.joinToString(", ")}")
