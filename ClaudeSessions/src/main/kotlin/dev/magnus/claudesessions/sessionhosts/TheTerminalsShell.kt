package dev.magnus.claudesessions.sessionhosts

import com.intellij.openapi.project.Project
import java.util.UUID
import org.jetbrains.plugins.terminal.TerminalProjectOptionsProvider

/**
 * The shell the IDE's terminal runs in [project], as its settings say, and the environment it gives it: what a
 * [SessionHost] runs in its console in place of the terminal, so that the session's shell is the one the user chose.
 *
 * The environment is the one the terminal plugin's `LocalOptionsConfigurer` builds in 2026.3, which is internal: the
 * IDE's own, unless the user unticked passing it in the terminal's settings; then `TERM`, `COLORTERM`,
 * `TERMINAL_EMULATOR`, which Claude Code reads, and a new `TERM_SESSION_ID`; then the user's own variables from the
 * terminal's settings. What it leaves out: the IDE launcher's variables put back as they were before it changed them,
 * which the platform's internal `EnvironmentRestorer` does, and what other plugins' terminal customizers add.
 */
class TheTerminalsShell(private val project: Project) {
    private val options: TerminalProjectOptionsProvider get() = TerminalProjectOptionsProvider.getInstance(project)

    /**
     * The shell's command line, as the terminal's settings say, without the arguments the terminal adds for its shell
     * integration. Off the EDT: when the setting names none, the terminal looks for the default shell, and waits.
     */
    val commandLine: String get() = options.shellPath

    /** The environment the terminal would give the shell. */
    fun environment(): Map<String, String> {
        val settings = options.getEnvData()
        return buildMap {
            if (settings.isPassParentEnvs) putAll(System.getenv())
            put("TERM", "xterm-256color")
            put("COLORTERM", "truecolor")
            put("TERMINAL_EMULATOR", "JetBrains-JediTerm")
            put("TERM_SESSION_ID", UUID.randomUUID().toString())
            putAll(settings.envs)
        }
    }
}
