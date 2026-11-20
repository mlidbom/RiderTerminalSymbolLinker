package dev.magnus.claudesessions.cloyd

import java.nio.file.Path
import kotlin.time.Duration

/** What came of a [CloydCommand]: the project's window shows in Cloyd, or why not. */
sealed interface CloydCommandOutcome {
    /** What came of it, for the IDE's log, and for the user when the window didn't show. */
    val description: String

    data object Shown : CloydCommandOutcome {
        override val description = "Cloyd shows the project's window."
    }

    /** No Cloyd is installed in the [CloydsFolder]. */
    data class NotInstalled(val cloydExe: Path) : CloydCommandOutcome {
        override val description get() = "Cloyd isn't installed: there is no $cloydExe."
    }

    /** The command failed, and said why in [output]. */
    data class Failed(val exitCode: Int, val output: String) : CloydCommandOutcome {
        override val description get() = "Cloyd.exe --project failed with exit code $exitCode: $output"
    }

    /** The command didn't end [within] the time it may take, and was ended. */
    data class DidNotEnd(val within: Duration) : CloydCommandOutcome {
        override val description get() = "Cloyd.exe --project didn't end within $within, and was ended."
    }
}
