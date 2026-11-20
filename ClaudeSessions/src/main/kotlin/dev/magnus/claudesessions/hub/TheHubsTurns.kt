package dev.magnus.claudesessions.hub

import com.intellij.openapi.diagnostic.thisLogger
import dev.magnus.claudesessions.claudecode.ClaudeCodeFolder
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
 * The running sessions' turns under way, as the hub tells of the text the model streams in them, on its stream of each,
 * `GET /running-sessions/{processId}/turn/stream`: for the chat of each tab, which shows it until the conversation has
 * it.
 *
 * refactor: following a stream of the hub's, from each hub the announcement names in turn, is
 * [TheHubsConversations.of]'s loop too. One class, such as a HubStream given the path and its reader, should do it for
 * both.
 */
class TheHubsTurns internal constructor(private val client: HttpClient) {
    /**
     * The block the model writes, or wrote last, in the turn of the Claude Code that runs as the process [processId], and
     * each time it changes: from the hub the announcement names, and, once its stream ends, as at a publish, from the
     * next, which tells of the block so far again. Only the latest is kept for a collector that is slower than the hub. A
     * Claude Code the hub doesn't know of yet, as one just started, is asked of again every few seconds.
     */
    fun streamedIn(processId: Long): Flow<TheHubsTurnStream.StreamedBlock?> = channelFlow {
        while (true) {
            val hub = HubAnnouncement.read()
            if (hub == null || !hub.speaksAProtocolThisBuildKnows || !hub.serves(ClaudeCodeFolder.path)) {
                delay(BETWEEN_TRIES)
                continue
            }
            val ended = runInterruptible(Dispatchers.IO) { follow(hub, processId) { trySendBlocking(it) } }
            delay(if (ended) BETWEEN_TRIES else ASKED_AGAIN_AFTER)
        }
    }.conflate()

    /**
     * Follows [hub]'s stream of the turn of [processId]'s Claude Code, telling each change of its block, until it ends:
     * true when it ended, or the hub wasn't there; false when the hub didn't stream it.
     */
    private fun follow(hub: HubAnnouncement, processId: Long, tell: (TheHubsTurnStream.StreamedBlock?) -> Unit): Boolean {
        try {
            val answer = client.send(hub.request("/running-sessions/$processId/turn/stream").GET().build(), HttpResponse.BodyHandlers.ofInputStream())
            answer.body().bufferedReader().use { lines ->
                // Not 200: a 404 for a Claude Code the hub doesn't know of yet, a 401 from another hub that took the port of the
                // one announced, which has stopped, or a 503 from a hub that stops.
                if (answer.statusCode() != 200) return false
                val stream = TheHubsTurnStream()
                val events = ServerSentEvents()
                while (true) {
                    val event = events.read(lines.readLine() ?: break) ?: continue
                    if (stream.read(event)) tell(stream.streamed)
                }
            }
        } catch (gone: IOException) {
            // As the contract says: the hub announced isn't there, as when it crashed, or it stopped while streaming, as at a
            // publish. Its announcement is read again. Or the tab's chat went, and interrupted the reading.
            if (gone.cause !is InterruptedException) thisLogger().info("The hub of build ${hub.build} isn't there, or stopped, as it streamed the turn of $processId: ${gone.message}")
        }
        return true
    }

    private companion object {
        /** How long to wait before reading the announcement again: a new hub announces itself a few tenths of a second after the old one stopped. */
        val BETWEEN_TRIES = 250.milliseconds

        /** How long to wait before asking again when the hub didn't stream the turn. */
        val ASKED_AGAIN_AFTER = 5.seconds
    }
}
