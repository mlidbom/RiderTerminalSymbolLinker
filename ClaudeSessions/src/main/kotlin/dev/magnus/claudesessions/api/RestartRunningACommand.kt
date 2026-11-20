package dev.magnus.claudesessions.api

import com.intellij.openapi.application.PathManager
import com.intellij.openapi.application.ex.ApplicationManagerEx
import com.intellij.openapi.diagnostic.thisLogger
import java.nio.file.Files
import java.nio.file.Path
import kotlin.io.path.createDirectories

/**
 * A restart of the IDE that runs [command] between the IDE's exit and its start, as the IDE's own restarter runs its
 * updater's patch: for a deploy that puts a build of a plugin in while the IDE is down, which a running IDE couldn't
 * take. The IDE starts its helper, [RunsACommandBetweenTheIdesExitAndItsStart], and exits; the helper waits for the
 * IDE's process to end, runs the command, and starts the IDE again, as its restart would: its executable, with no
 * arguments, in the IDE's own environment. The IDE then reopens the projects it had open.
 *
 * The platform's own way to have its restarter run a command, `ApplicationEx.restart(flags, beforeRestart)` and
 * `Restarter`, is internal API, which the Plugin Verifier refuses.
 *
 * The helper runs on the IDE's Java runtime, from a copy of its class in the IDE's system folder, outside the plugin,
 * whose files the command may replace; what it says, and what the command prints, go to
 * [LOG], in the IDE's log folder.
 */
class RestartRunningACommand(private val command: List<String>) {
    /** Starts the helper, and has the IDE exit, without asking the user to confirm it. */
    fun start() {
        thisLogger().info("Restarting the IDE, running $command before it starts again; the helper's log is $LOG")
        val ide = ProcessHandle.current()
        val helper = ProcessBuilder(
            listOf(javaOfTheIde().toString(), "-cp", copyOfTheHelper().toString(), RunsACommandBetweenTheIdesExitAndItsStart::class.java.name)
                + listOf(ide.pid().toString(), ide.info().command().orElseThrow(), SECONDS_THE_EXIT_MAY_TAKE.toString())
                + command,
        )
        helper.redirectErrorStream(true).redirectOutput(ProcessBuilder.Redirect.appendTo(LOG.toFile())).start()
        ApplicationManagerEx.getApplicationEx().exit(false, true)
    }

    private fun javaOfTheIde(): Path = Path.of(System.getProperty("java.home"), "bin", "java")

    /** Copies the helper's class out of the plugin, into a folder of its own, and returns the folder, for its class path. */
    private fun copyOfTheHelper(): Path {
        val classPath = Path.of(PathManager.getSystemPath(), "claude-sessions", "restart-helper")
        val classFile = RunsACommandBetweenTheIdesExitAndItsStart::class.java.name.replace('.', '/') + ".class"
        val copy = classPath.resolve(classFile).also { it.parent.createDirectories() }
        javaClass.classLoader.getResourceAsStream(classFile)!!.use { Files.write(copy, it.readAllBytes()) }
        return classPath
    }

    companion object {
        /** How long the helper waits for the IDE to exit, before it gives up, running nothing and starting nothing. */
        const val SECONDS_THE_EXIT_MAY_TAKE = 300

        /** Where the helper says what it did, and the command what it printed. */
        val LOG: Path get() = Path.of(PathManager.getLogPath(), "claude-sessions-restarts.log")
    }
}
