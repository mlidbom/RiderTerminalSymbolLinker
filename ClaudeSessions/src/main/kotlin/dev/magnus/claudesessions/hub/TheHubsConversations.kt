package dev.magnus.claudesessions.hub

import com.intellij.openapi.diagnostic.thisLogger
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.claudecode.ClaudeCodeFolder
import dev.magnus.claudesessions.claudecode.ClaudeSessionId
import java.io.IOException
import java.net.http.HttpClient
import java.net.http.HttpResponse
import kotlin.time.Duration.Companion.milliseconds
import kotlin.time.Duration.Companion.seconds
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.channels.trySendBlocking
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.channelFlow
import kotlinx.coroutines.flow.conflate
import kotlinx.coroutines.runInterruptible

/**
 * The sessions' conversations, as the hub tells of them on its stream of each, `GET /sessions/{id}/conversation/stream`:
 * for the chat of each tab.
 */
class TheHubsConversations internal constructor(private val client: HttpClient) {
    /**
     * The conversation of [session]'s line, as it is, and each time it changes: from the hub the announcement names,
     * and, once its stream ends, as at a publish, from the next, which tells of all of it again. Only the latest is kept
     * for a collector that is slower than the hub. A session the hub knows nothing of, with no transcript and no Claude
     * Code that runs it, has no conversation yet: the hub is asked again every few seconds.
     */
    fun of(session: ClaudeSessionId): Flow<ConversationOfALine> = channelFlow {
        while (true) {
            val hub = HubAnnouncement.read()
            if (hub == null || !hub.speaksAProtocolThisBuildKnows || !hub.serves(ClaudeCodeFolder.path)) {
                delay(BETWEEN_TRIES)
                continue
            }
            when (runInterruptible(Dispatchers.IO) { follow(hub, session) { trySendBlocking(it) } }) {
                StreamEnd.NO_SUCH_SESSION -> {
                    send(ConversationOfALine(Conversation(emptyList()), setOf(session)))
                    delay(ASKED_AGAIN_AFTER)
                }
                StreamEnd.REFUSED -> delay(ASKED_AGAIN_AFTER)
                StreamEnd.ENDED -> delay(BETWEEN_TRIES)
            }
        }
    }.conflate()

    /** Follows [hub]'s stream of [session]'s conversation, telling each [ConversationOfALine] it tells of, until it ends. */
    private fun follow(hub: HubAnnouncement, session: ClaudeSessionId, tell: (ConversationOfALine) -> Unit): StreamEnd {
        try {
            val answer = client.send(hub.request("/sessions/${session.value}/conversation/stream").GET().build(), HttpResponse.BodyHandlers.ofInputStream())
            answer.body().bufferedReader().use { lines ->
                when (answer.statusCode()) {
                    200 -> {}
                    404 -> return StreamEnd.NO_SUCH_SESSION
                    400 -> error("The hub took the session ${session.value} for no session's id: ${lines.readText()}")
                    // A 401 from another hub that took the port of the one announced, which has stopped, or a 503 from a hub that stops.
                    else -> return StreamEnd.REFUSED
                }
                val stream = TheHubsConversationStream()
                val events = ServerSentEvents()
                while (true) {
                    val event = events.read(lines.readLine() ?: break) ?: continue
                    stream.read(event)?.let(tell)
                }
            }
        } catch (gone: IOException) {
            // As the contract says: the hub announced isn't there, as when it crashed, or it stopped while streaming, as at a
            // publish. Its announcement is read again. Or the tab's chat went, and interrupted the reading.
            if (gone.cause !is InterruptedException) thisLogger().info("The hub of build ${hub.build} isn't there, or stopped, as it streamed the conversation of ${session.value}: ${gone.message}")
        }
        return StreamEnd.ENDED
    }

    /** How a stream of a conversation ended. */
    private enum class StreamEnd {
        /** The hub knows nothing of the session. */
        NO_SUCH_SESSION,

        /** The hub didn't stream it: another hub took the port, or the hub stops. */
        REFUSED,

        /** The stream ended, or the hub wasn't there. */
        ENDED,
    }

    private companion object {
        /** How long to wait before reading the announcement again: a new hub announces itself a few tenths of a second after the old one stopped. */
        val BETWEEN_TRIES = 250.milliseconds

        /** How long to wait before asking again of a session the hub knew nothing of, or when it refused. */
        val ASKED_AGAIN_AFTER = 5.seconds
    }
}
