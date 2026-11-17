package dev.magnus.claudesessions.api

import com.google.gson.GsonBuilder
import com.google.gson.JsonObject
import java.nio.file.Files
import java.nio.file.Path
import java.nio.file.StandardCopyOption

/**
 * The file that tells other tools where an IDE's [LocalApi] listens: `ClaudeSessions\ides\<the IDE's process id>.json`
 * in the user's local application data folder, which names the protocol, the process, and the port. A tool that knows
 * the IDE's process, as one that brings its window forward does, finds the file by it.
 *
 * The IDE writes it as the API starts, and deletes it as the API stops. One left behind by a crash names a process that
 * no longer runs, or a port nothing listens on: a refused connection tells a tool that the API isn't there.
 *
 * `protocol` is 1. It changes when the file or the requests change in a way that a tool written for an earlier one
 * would get wrong.
 */
class LocalApiAnnouncement private constructor(private val process: ProcessHandle, private val file: Path) {
    /** Writes the file, saying that the API listens on [port]: at once, so that a tool never reads half of it. */
    fun announce(port: Int) {
        val json = JsonObject().apply {
            addProperty("protocol", PROTOCOL)
            addProperty("processId", process.pid())
            addProperty("port", port)
        }
        Files.createDirectories(file.parent)
        val written = file.resolveSibling("${file.fileName}.writing")
        Files.writeString(written, gson.toJson(json))
        Files.move(written, file, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
    }

    /** Deletes the file, as the API stops. */
    fun withdraw() {
        Files.deleteIfExists(file)
    }

    companion object {
        /** The version of the file and the requests. */
        const val PROTOCOL = 1

        private val gson = GsonBuilder().setPrettyPrinting().create()

        /**
         * The announcement of the API of the IDE that runs as [process]. On Windows, the local application data folder
         * is the one `LOCALAPPDATA` names; elsewhere, `~/.local/state`.
         */
        fun of(process: ProcessHandle): LocalApiAnnouncement {
            val folder = System.getenv("LOCALAPPDATA")?.let { Path.of(it) } ?: Path.of(System.getProperty("user.home"), ".local", "state")
            return LocalApiAnnouncement(process, folder.resolve("ClaudeSessions").resolve("ides").resolve("${process.pid()}.json"))
        }
    }
}
