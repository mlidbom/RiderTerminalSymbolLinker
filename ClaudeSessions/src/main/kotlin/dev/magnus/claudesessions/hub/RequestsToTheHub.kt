package dev.magnus.claudesessions.hub

import com.google.gson.JsonObject
import dev.magnus.claudesessions.claudecode.ClaudeCodeFolder
import java.net.ConnectException
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.time.Duration
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runInterruptible

/**
 * Requests of the hub's local API that must reach a hub, such as typing into a session or starting a session host: each
 * goes to the hub the [HubAnnouncement] names when it's sent, and when none is announced the published hub is started,
 * once, as [ThePublishedHub] says. The contract is `~/.claude/dev_docs/the-hubs-local-api.md`.
 *
 * At a publish of a new build, the hub asked stops: it refuses the connection, or answers 503 while the request waits,
 * or another hub, which took its port, answers 401. So the request goes to the hub the announcement names then, as the
 * contract says, until one answers or [LOOKING_FOR_A_HUB_MAY_TAKE] has passed.
 */
internal class RequestsToTheHub(private val client: HttpClient) {
    /** Posts [body], or none, to [path], and returns the answer of the hub that answered it within [answeringMayTake]. */
    suspend fun post(path: String, body: JsonObject?, answeringMayTake: Duration): HubAnswer =
        send(path, answeringMayTake) { it.header("Content-Type", "application/json").POST(body?.let { json -> HttpRequest.BodyPublishers.ofString(json.toString()) } ?: HttpRequest.BodyPublishers.noBody()) }

    /** Gets [path], and returns the answer of the hub that answered it within [answeringMayTake]. */
    suspend fun get(path: String, answeringMayTake: Duration): HubAnswer = send(path, answeringMayTake) { it.GET() }

    /** Sends the request [method] makes of [path], on a thread it blocks, and which an interrupt ends. */
    private suspend fun send(path: String, answeringMayTake: Duration, method: (HttpRequest.Builder) -> HttpRequest.Builder): HubAnswer =
        runInterruptible(Dispatchers.IO) { sendLookingForAHub { hub -> method(hub.request(path).timeout(answeringMayTake)).build() } }

    private fun sendLookingForAHub(request: (HubAnnouncement) -> HttpRequest): HubAnswer {
        val deadline = System.nanoTime() + LOOKING_FOR_A_HUB_MAY_TAKE.toNanos()
        var startedThePublishedHub = false
        while (true) {
            val hub = HubAnnouncement.read()
            when {
                hub == null && !startedThePublishedHub && ThePublishedHub.isTheOneThisIdeFollows -> {
                    ThePublishedHub.start()
                    startedThePublishedHub = true
                }
                hub == null -> {}
                !hub.speaksAProtocolThisBuildKnows ->
                    return HubAnswer.NoHubAnswered("The Claude tooling's hub speaks protocol ${hub.protocol}, which this build of Claude Sessions doesn't know: restart the IDE.")
                !hub.serves(ClaudeCodeFolder.path) ->
                    return HubAnswer.NoHubAnswered("The Claude tooling's hub serves the Claude Code folder ${hub.claudeCodeFolder}, and this IDE runs Claude Code with ${ClaudeCodeFolder.path}.")
                else -> answerOf(hub, request(hub))?.let { return it }
            }
            if (System.nanoTime() > deadline) return HubAnswer.NoHubAnswered("No Claude tooling hub answered within ${LOOKING_FOR_A_HUB_MAY_TAKE.toSeconds()} seconds.")
            Thread.sleep(BETWEEN_TRIES.toMillis())
        }
    }

    /** The answer of [hub] to [request], or null when [hub] is no longer there to answer it, as at a publish. */
    private fun answerOf(hub: HubAnnouncement, request: HttpRequest): HubAnswer? {
        val answer = try {
            client.send(request, HttpResponse.BodyHandlers.ofString())
        } catch (_: ConnectException) {
            return null
        }
        if (answer.statusCode() == 401 || answer.statusCode() == 503) return null
        return HubAnswer.Answered(answer.statusCode(), answer.body(), hub.build)
    }

    private companion object {
        /** How long a request looks for a hub to answer it: one starting, or the next build's at a publish. */
        val LOOKING_FOR_A_HUB_MAY_TAKE: Duration = Duration.ofSeconds(30)

        val BETWEEN_TRIES: Duration = Duration.ofMillis(250)
    }
}
