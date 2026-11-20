package dev.magnus.claudesessions.cloyd

import java.nio.file.Files
import java.nio.file.Path
import java.util.concurrent.TimeUnit
import kotlin.time.Duration
import kotlin.time.Duration.Companion.minutes

/**
 * `Cloyd.exe --project <folder>`, the command that hands a project to the Cloyd application, which shows the project's
 * window: brought forward when it's open, else opened. The command starts the application when none runs, outside the
 * IDE's job, so that it outlives the IDE, and exits once the window shows: in a fraction of a second when the application
 * runs, and in about a second when the command starts it.
 */
class CloydCommand(private val cloyd: CloydsFolder) {
    /**
     * Has the Cloyd application show the window of the project in [projectFolder], and returns what came of it. The
     * window comes in front of the IDE's when the IDE is the application in front, as it is when the user opens the
     * project: the command lets the Cloyd application bring it there.
     *
     * Its output is read from a file, not a pipe, so that a command that hangs can't keep this waiting past [HOW_LONG_IT_MAY_TAKE].
     */
    fun showTheWindowOf(projectFolder: Path): CloydCommandOutcome {
        val cloydExe = cloyd.cloydExe
        if (!Files.exists(cloydExe)) return CloydCommandOutcome.NotInstalled(cloydExe)

        val output = Files.createTempFile("cloyd-command-", ".txt")
        try {
            val command = ProcessBuilder(cloydExe.toString(), "--project", projectFolder.toString())
                .redirectErrorStream(true)
                .redirectOutput(output.toFile())
                .start()
            if (!command.waitFor(HOW_LONG_IT_MAY_TAKE.inWholeSeconds, TimeUnit.SECONDS)) {
                command.destroyForcibly()
                return CloydCommandOutcome.DidNotEnd(HOW_LONG_IT_MAY_TAKE)
            }
            return if (command.exitValue() == 0) CloydCommandOutcome.Shown else CloydCommandOutcome.Failed(command.exitValue(), Files.readString(output))
        } finally {
            Files.delete(output)
        }
    }

    private companion object {
        /** The command gives up starting the application after 30 seconds, and after a minute's wait for another IDE's command. */
        val HOW_LONG_IT_MAY_TAKE: Duration = 2.minutes
    }
}
