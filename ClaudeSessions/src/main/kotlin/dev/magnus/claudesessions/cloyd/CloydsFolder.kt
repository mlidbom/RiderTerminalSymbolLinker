package dev.magnus.claudesessions.cloyd

import java.nio.file.Path

/**
 * Cloyd's folder: where Cloyd, the application beside the IDEs that shows the projects' Claude Code sessions, is
 * installed, in `bin`, and where it keeps what it saves and announces its local API. `%LOCALAPPDATA%\Cloyd`, or the
 * folder `CLOYD_FOLDER` names, as the sandbox IDE's environment does, for a Cloyd of its own.
 */
class CloydsFolder(private val folder: Path) {
    /** The executable that runs [CloydCommand], and the Cloyd application it starts. */
    val cloydExe: Path get() = folder.resolve("bin").resolve("Cloyd.exe")

    override fun toString(): String = folder.toString()

    companion object {
        private const val VARIABLE_NAMING_ANOTHER = "CLOYD_FOLDER"

        /** The folder the IDE's environment names: see [CloydsFolder]. */
        fun ofTheIdesEnvironment(): CloydsFolder =
            CloydsFolder(System.getenv(VARIABLE_NAMING_ANOTHER)?.takeIf { it.isNotEmpty() }?.let(Path::of) ?: Path.of(System.getenv("LOCALAPPDATA"), "Cloyd"))
    }
}
