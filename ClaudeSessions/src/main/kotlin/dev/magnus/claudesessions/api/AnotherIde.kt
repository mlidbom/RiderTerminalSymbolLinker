package dev.magnus.claudesessions.api

import com.google.gson.JsonParser
import com.intellij.openapi.util.SystemInfo
import com.sun.jna.Native
import com.sun.jna.win32.StdCallLibrary
import dev.magnus.claudesessions.ClaudeSessionId
import java.net.ConnectException
import java.net.URI
import java.net.URLEncoder
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.net.http.HttpTimeoutException
import java.nio.file.Files
import java.nio.file.NoSuchFileException
import java.nio.file.Path
import java.time.Duration

/**
 * Another IDE on the machine that runs Claude Sessions, as its [LocalApiAnnouncement] names it: its process, and the
 * port its [LocalApi] listens on. Through the API, this IDE learns which sessions run in the other's tabs, and has it
 * show one.
 */
class AnotherIde private constructor(val processId: Long, private val port: Int) {
    /**
     * The sessions that run in the other IDE's tabs, each with its Claude Code's process. None when its API doesn't
     * answer: an IDE that crashed leaves its announcement behind, and one that is busy may not answer in time.
     */
    fun sessionsInItsTabs(): List<RunningInATab> {
        val answer = send(HttpRequest.newBuilder(uri("/sessions")).GET()) ?: return emptyList()
        if (answer.statusCode() != 200) return emptyList()
        return JsonParser.parseString(answer.body()).asJsonObject.getAsJsonArray("sessions").map { it.asJsonObject }.map {
            RunningInATab(ClaudeSessionId(it.get("session").asString), it.get("process").asLong)
        }
    }

    /**
     * Has the other IDE show the tab in which Claude Code runs [session] as [process], and bring its window forward,
     * which this IDE, the active application, allows it first. Returns whether it showed one.
     */
    fun show(session: ClaudeSessionId, process: Long): Boolean {
        allowToComeForward()
        val query = "session=${URLEncoder.encode(session.value, Charsets.UTF_8)}&process=$process"
        return send(HttpRequest.newBuilder(uri("/sessions/show?$query")).POST(HttpRequest.BodyPublishers.noBody()))?.statusCode() == 200
    }

    /** A session that runs in one of the other IDE's tabs, and its Claude Code's process. */
    data class RunningInATab(val session: ClaudeSessionId, val process: Long)

    /**
     * Allows the other IDE to bring its window forward. Windows lets no application that isn't active do so, unless the
     * active one allows it.
     */
    private fun allowToComeForward() {
        if (SystemInfo.isWindows) User32.INSTANCE.AllowSetForegroundWindow(processId.toInt())
    }

    private fun uri(pathAndQuery: String): URI = URI.create("http://127.0.0.1:$port$pathAndQuery")

    /** Sends [request], and returns the answer, or null when the API isn't there, or doesn't answer in time. */
    private fun send(request: HttpRequest.Builder): HttpResponse<String>? = try {
        client.send(request.timeout(TIMEOUT).build(), HttpResponse.BodyHandlers.ofString())
    } catch (_: ConnectException) {
        null
    } catch (_: HttpTimeoutException) {
        null
    }

    private interface User32 : StdCallLibrary {
        @Suppress("FunctionName")
        fun AllowSetForegroundWindow(processId: Int): Boolean

        companion object {
            val INSTANCE: User32 by lazy { Native.load("user32", User32::class.java) }
        }
    }

    companion object {
        private val TIMEOUT: Duration = Duration.ofSeconds(2)

        /** HTTP/1.1, as the API speaks, so that it isn't first asked to upgrade to HTTP/2. */
        private val client: HttpClient = HttpClient.newBuilder().version(HttpClient.Version.HTTP_1_1).connectTimeout(TIMEOUT).build()

        /**
         * The other IDEs whose announcements name a process that runs: not this IDE, and not one whose announcement a
         * crash left behind.
         */
        fun all(): List<AnotherIde> {
            val folder = LocalApiAnnouncement.folder
            if (!Files.isDirectory(folder)) return emptyList()
            val announcements = Files.newDirectoryStream(folder, "*.json").use { it.toList() }
            val thisIde = ProcessHandle.current().pid()
            return announcements.mapNotNull(::read).filter { it.processId != thisIde && ProcessHandle.of(it.processId).isPresent }
        }

        /**
         * The IDE [announcement] names, or null when it's of another protocol, or gone, withdrawn as the IDE's API
         * stopped. The IDE writes it whole at once, so it's never read half-written.
         */
        private fun read(announcement: Path): AnotherIde? {
            val json = try {
                JsonParser.parseString(Files.readString(announcement)).asJsonObject
            } catch (_: NoSuchFileException) {
                return null
            }
            if (json.get("protocol")?.asInt != LocalApiAnnouncement.PROTOCOL) return null
            return AnotherIde(json.get("processId").asLong, json.get("port").asInt)
        }
    }
}
