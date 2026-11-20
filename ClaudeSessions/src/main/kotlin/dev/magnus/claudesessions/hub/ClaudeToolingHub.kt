package dev.magnus.claudesessions.hub

import com.intellij.notification.NotificationType
import com.intellij.openapi.Disposable
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.diagnostic.thisLogger
import dev.magnus.claudesessions.ClaudeSessionsNotifications
import dev.magnus.claudesessions.claudecode.ClaudeCodeFolder
import dev.magnus.claudesessions.claudecode.RunningClaudeCodes
import java.io.IOException
import java.net.http.HttpClient
import java.net.http.HttpResponse
import java.time.Duration
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.runInterruptible
import kotlin.time.Duration.Companion.milliseconds

/**
 * The Claude tooling's hub, ClaudeTooling's resident server, as the IDE follows it: the one place in the plugin that
 * talks to it. The hub owns everything about Claude Code, as Magnus and the tooling owner agreed on 2026-11-19
 * (`~/.claude/dev_docs/the-claude-tooling-bundle.md`): which Claude Codes run, with their sessions, which the plugin
 * follows on the stream of its local API, `GET /running-sessions/stream`, all [typing] into Claude Code and the shell
 * below it, and the [sessionHosts] the sessions run in.
 *
 * Every Claude Code session's SessionStart hook starts the hub, and it stops only once no session has run for minutes.
 * So when no hub runs, no Claude Code does, and the plugin waits for one to announce itself, unless it must type into
 * a shell first: then it starts [ThePublishedHub].
 *
 * The hub is republished several times a day, under running sessions: the old hub ends its streams and stops, and the
 * new build's announces itself, on a port and with a token of its own, a few tenths of a second later. So when a
 * stream ends, or a hub isn't there, the plugin reads the [HubAnnouncement] again, and follows the hub it names then.
 */
@Service(Service.Level.APP)
class ClaudeToolingHub(scope: CoroutineScope) : Disposable {
    /** HTTP/1.1, as the hub speaks, so that it isn't first asked to upgrade to HTTP/2. */
    private val client: HttpClient = HttpClient.newBuilder().version(HttpClient.Version.HTTP_1_1).connectTimeout(CONNECTING_MAY_TAKE).build()

    private val told = MutableStateFlow(RunningClaudeCodes.NONE)

    /** The Claude Codes that run, as the hub last told of them: none until it has. */
    val runningClaudeCodes: StateFlow<RunningClaudeCodes> = told.asStateFlow()

    private val requests = RequestsToTheHub(client)

    /** Typing into Claude Code, and into the shell below it, through the hub. */
    val typing = TypingIntoClaudeCode(requests)

    /** The session hosts, outside the IDE, that the tabs' sessions run in, as the hub starts, lists and ends them. */
    val sessionHosts = SessionHosts(requests)

    /** The sessions' conversations, as the hub tells of them, for the tabs' chats. */
    val conversations = TheHubsConversations(client)

    /** The text the model streams in the running sessions' turns, as the hub tells of it, for the tabs' chats. */
    val turns = TheHubsTurns(client)

    /** The builds of the hubs the user has been told about, each once: one of a protocol this build doesn't know, or of another Claude Code folder. */
    private val toldAbout = mutableSetOf<String>()

    init {
        scope.launch(Dispatchers.IO) { followEachHubInTurn() }
    }

    override fun dispose() {
        client.shutdownNow()
    }

    /**
     * Follows each hub the announcement names, in turn, until the plugin unloads or the IDE exits. Following one blocks
     * its thread, reading the stream, and is interrupted as the plugin's scope is cancelled: an unload waits for that.
     * Without the interrupt, the reading went on until the hub ended its stream, and the sandbox IDE froze in the unload
     * (2026-11-19).
     */
    private suspend fun followEachHubInTurn() {
        while (true) {
            HubAnnouncement.read()?.let { runInterruptible { follow(it) } }
            delay(BETWEEN_TRIES)
        }
    }

    /** Follows [hub] until its stream ends, if it speaks a protocol this build knows, and serves the IDE's Claude Code folder. */
    private fun follow(hub: HubAnnouncement) {
        when {
            !hub.speaksAProtocolThisBuildKnows -> tellToRestartTheIde(hub.build, hub.protocol.toString())
            !hub.serves(ClaudeCodeFolder.path) -> logOnce(hub.build) { "The hub's API serves the Claude Code folder ${hub.claudeCodeFolder}, and this IDE runs Claude Code with ${ClaudeCodeFolder.path}: not followed." }
            else -> followTheRunningSessionsOf(hub)
        }
    }

    /** Follows the stream of the running sessions of [hub], until it ends, as the hub stops, or [hub] isn't there. */
    private fun followTheRunningSessionsOf(hub: HubAnnouncement) {
        try {
            val answer = client.send(hub.request("/running-sessions/stream").GET().build(), HttpResponse.BodyHandlers.ofInputStream())
            answer.body().bufferedReader().use { lines ->
                // Not 200: a 401 from another hub that took the port of the one announced, which has stopped, or a 503 from a hub that stops.
                if (answer.statusCode() != 200) return
                val protocol = answer.headers().firstValue(HubProtocol.HEADER).orElse(null)
                if (protocol != HubProtocol.VERSION.toString()) return tellToRestartTheIde(hub.build, protocol)
                thisLogger().info("Following the running sessions of the hub of build ${hub.build}.")
                val events = ServerSentEvents()
                while (true) {
                    val event = events.read(lines.readLine() ?: break) ?: continue
                    if (event.name == RUNNING_SESSIONS) told.value = RunningSessionsAnswer.parse(event.data).runningClaudeCodes()
                }
                thisLogger().info("The hub of build ${hub.build} ended its stream of the running sessions.")
            }
        } catch (gone: IOException) {
            if (gone.cause is InterruptedException) return // The plugin unloads, or the IDE exits: see followEachHubInTurn.
            // As the contract says: the hub announced isn't there, as when it crashed, or it stopped while streaming, as at
            // a publish. Read its announcement again.
            thisLogger().info("The hub of build ${hub.build} isn't there, or stopped: ${gone.message}")
        }
    }

    /** Tells the user, once for each hub, that the hub of [build] speaks [protocol], which this build doesn't know. */
    private fun tellToRestartTheIde(build: String, protocol: String?) {
        if (!toldAbout.add(build)) return
        ClaudeSessionsNotifications.tell(
            project = null,
            "Claude Sessions can't follow the Claude tooling's hub",
            "The hub speaks protocol $protocol, and this build of Claude Sessions knows protocol ${HubProtocol.VERSION}, so its tabs can't tell which " +
                "sessions run in them. Restart the IDE to run a build that knows it.",
            NotificationType.WARNING,
        )
    }

    private fun logOnce(build: String, message: () -> String) {
        if (toldAbout.add(build)) thisLogger().warn(message())
    }

    companion object {
        private val CONNECTING_MAY_TAKE: Duration = Duration.ofSeconds(2)

        /** How long to wait before reading the announcement again: a new hub announces itself a few tenths of a second after the old one stopped. */
        private val BETWEEN_TRIES = 250.milliseconds

        /** The name of the stream's events that tell of the running sessions. */
        private const val RUNNING_SESSIONS = "running-sessions"

        fun getInstance(): ClaudeToolingHub = service()
    }
}
