package dev.magnus.claudesessions.hub

import com.google.gson.JsonObject
import com.google.gson.JsonParser
import java.net.URI
import java.net.http.HttpRequest
import java.nio.file.Files
import java.nio.file.NoSuchFileException
import java.nio.file.Path

/**
 * The file in which the Claude tooling's hub, its resident server, tells where its local API listens, `local-api.json`:
 * the version of its protocol, its port, the token every request must carry, its build, and the Claude Code folder whose
 * sessions it serves. The hub writes it once its API listens, and deletes it as it stops, as at each publish of a new
 * build, whose hub then writes its own, with a port and a token of its own.
 *
 * Only what this build of the plugin knows is read of it, and the rest only once [protocol] is one it knows: a hub of a
 * later protocol may say the rest otherwise.
 */
class HubAnnouncement private constructor(private val json: JsonObject) {
    /** The version of the hub's protocol: see [HubProtocol]. */
    val protocol: Int = json.get("protocol").asInt

    /** Whether this build of the plugin knows the hub's [protocol]. */
    val speaksAProtocolThisBuildKnows: Boolean get() = protocol == HubProtocol.VERSION

    /** The hub's build, the time it was published: each publish starts a hub of a new one. */
    val build: String get() = json.get("build").asString

    private val port: Int get() = json.get("port").asInt

    private val token: String get() = json.get("token").asString

    /** The Claude Code folder whose sessions the hub serves: one hub runs for each. */
    val claudeCodeFolder: Path get() = Path.of(json.get("claudeCodeFolder").asString)

    /** Whether the hub serves the sessions of [folder], the Claude Code folder the IDE's tabs run Claude Code with. */
    fun serves(folder: Path): Boolean = claudeCodeFolder.toAbsolutePath().normalize() == folder.toAbsolutePath().normalize()

    /** A request to the hub's API for [path], such as `/running-sessions`, with the token it takes. */
    fun request(path: String): HttpRequest.Builder =
        HttpRequest.newBuilder(URI.create("http://127.0.0.1:$port$path")).header("Authorization", "Bearer $token")

    companion object {
        /**
         * The environment variable that names the folder of the announcement of a hub tried out apart from the published
         * one, as the sandbox IDE runs one for a Claude Code folder of its own.
         */
        private const val VARIABLE_NAMING_THE_FOLDER = "CLAUDE_TOOLING_LOCAL_API_FOLDER"

        private val trialHubsFolder: Path? get() = System.getenv(VARIABLE_NAMING_THE_FOLDER)?.takeIf { it.isNotEmpty() }?.let { Path.of(it) }

        /** Whether the announcement read is of a hub tried out apart from the published one: [VARIABLE_NAMING_THE_FOLDER] names its folder. */
        val isOfATrialHub: Boolean get() = trialHubsFolder != null

        /**
         * The folder of the announcement: the one [VARIABLE_NAMING_THE_FOLDER] names, or else the published hub's,
         * `%LOCALAPPDATA%\ClaudeTooling`. Null where neither is set, as outside Windows, where no hub runs.
         */
        private val folder: Path?
            get() = trialHubsFolder ?: System.getenv("LOCALAPPDATA")?.let { Path.of(it, "ClaudeTooling") }

        /**
         * The announcement of the hub that runs now, or null when none does. The hub writes it whole at once, so it's
         * never read half-written. A hub that crashed leaves its announcement behind, naming a port nothing listens on.
         */
        fun read(): HubAnnouncement? {
            val file = folder?.resolve("local-api.json") ?: return null
            val text = try {
                Files.readString(file)
            } catch (_: NoSuchFileException) {
                return null
            }
            return parse(text)
        }

        /** The announcement [text] says, as the hub writes it. */
        fun parse(text: String): HubAnnouncement = HubAnnouncement(JsonParser.parseString(text).asJsonObject)
    }
}
