package dev.magnus.claudesessions.history

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.execution.configurations.PathEnvironmentVariableUtil
import com.intellij.execution.util.ExecUtil
import java.nio.file.Path

/**
 * The folders the [ClaudeSessionHistoryScope]s cover for a project: the git worktree the project is in, and every
 * worktree of its repository, as git lists them. A project that isn't in git has only its own folder, and no
 * [ClaudeSessionHistoryScope.ALL_WORKTREES].
 *
 * @property thisWorktree The project's worktree, or its folder when it isn't in git.
 * @property allWorktrees Every worktree of the project's repository, or null when it isn't in git.
 */
class ProjectWorktrees(val thisWorktree: Path, val allWorktrees: List<Path>?) {
    /** The scopes the picker offers the project. */
    val scopes: List<ClaudeSessionHistoryScope>
        get() = ClaudeSessionHistoryScope.entries.filter { it != ClaudeSessionHistoryScope.ALL_WORKTREES || allWorktrees != null }

    /** Whether [scope] covers [folder]: whether it's in or below one of the scope's folders. */
    fun cover(scope: ClaudeSessionHistoryScope, folder: Path): Boolean = when (scope) {
        ClaudeSessionHistoryScope.THIS_WORKTREE -> folder.isInOrBelow(thisWorktree)
        ClaudeSessionHistoryScope.ALL_WORKTREES -> allWorktrees!!.any { folder.isInOrBelow(it) }
        ClaudeSessionHistoryScope.ALL_PROJECTS -> true
    }

    private fun Path.isInOrBelow(folder: Path): Boolean = toAbsolutePath().normalize().startsWith(folder.toAbsolutePath().normalize())

    companion object {
        /** The worktrees of the project whose folder is [projectFolder], as git tells: it runs git, so not on the EDT. */
        fun of(projectFolder: Path): ProjectWorktrees {
            val git = PathEnvironmentVariableUtil.findFirst("git")?.toString() ?: return ProjectWorktrees(projectFolder, null)
            val worktree = git(git, projectFolder, "rev-parse", "--show-toplevel")?.singleOrNull()
                ?: return ProjectWorktrees(projectFolder, null)
            val worktrees = git(git, projectFolder, "worktree", "list", "--porcelain").orEmpty()
                .filter { it.startsWith("worktree ") }
                .map { Path.of(it.removePrefix("worktree ")) }
            return ProjectWorktrees(Path.of(worktree), worktrees)
        }

        /** What git prints, a line each, or null when it fails: outside a git repository, it does. */
        private fun git(git: String, folder: Path, vararg arguments: String): List<String>? {
            val output = ExecUtil.execAndGetOutput(GeneralCommandLine(git, "-C", folder.toString(), *arguments))
            return if (output.exitCode == 0) output.stdoutLines else null
        }
    }
}
