package dev.magnus.claudesessions.hub

import java.net.URI
import java.nio.file.Path
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Whether the plugin reads the hub's local API as the hub answers it: on copies of the samples of its answers that the
 * hub's own specifications compare its answers with, in `~/.claude/src/ClaudeTooling.Specifications/ResidentServer/
 * LocalApi/Samples`, copied on 2026-11-19 from the tooling's commit cc7853b. When the hub's samples change, as its
 * contract, `~/.claude/dev_docs/the-hubs-local-api.md`, changes, copy them again.
 */
class TheHubsLocalApiContractTest {
    @Test
    fun `the announcement names the hub's protocol, its port and token, its build, and its Claude Code folder`() {
        val announcement = HubAnnouncement.parse(HttpSample.named("local-api.json"))

        assertEquals(1, announcement.protocol)
        assertTrue(announcement.speaksAProtocolThisBuildKnows)
        assertEquals("2026-11-19T18:04:16.382911Z", announcement.build)
        assertTrue(announcement.serves(Path.of("C:\\Users\\magnu\\.claude")))
        assertFalse(announcement.serves(Path.of("C:\\Users\\magnu\\AppData\\Local\\RiderTerminalSymbolLinkerSandbox\\claude-code")))

        val request = announcement.request("/running-sessions").build()
        assertEquals(URI.create("http://127.0.0.1:54308/running-sessions"), request.uri())
        assertEquals("Bearer WpLE91DTEXYCOydCmC8qcwSqgFithpWBNqV7UsPnwe4", request.headers().firstValue("Authorization").orElse(null))
    }

    @Test
    fun `the running sessions are read with each one's Claude Code's process, its session, its name and its directory`() {
        val answer = HttpSample(HttpSample.named("running-sessions.http"))

        assertEquals(200, answer.status)
        assertEquals(HubProtocol.VERSION.toString(), answer.header(HubProtocol.HEADER))
        assertEquals(
            listOf(
                RunningSessionInTheAnswer(24936, "87c58397-ae2b-4346-ab49-ebc624ed1193", "chat-view", "C:\\Dev\\ClaudeSymbolLinker"),
                RunningSessionInTheAnswer(61812, "34aba8f2-a171-443f-955b-d2733a92adf2", null, "C:\\Users\\magnu\\.claude"),
            ),
            RunningSessionsAnswer.parse(answer.body).runningSessions,
        )
    }

    @Test
    fun `the stream's events each tell of the running sessions anew, and its keep-alive is no event`() {
        val stream = HttpSample(HttpSample.named("running-sessions-stream.http"))
        val reading = ServerSentEvents()
        val events = stream.bodyLines.mapNotNull(reading::read)

        assertEquals(200, stream.status)
        assertEquals("text/event-stream", stream.header("Content-Type"))
        assertEquals(HubProtocol.VERSION.toString(), stream.header(HubProtocol.HEADER))
        assertEquals(listOf("running-sessions", "running-sessions"), events.map { it.name })
        assertEquals(listOf(listOf(61812L), listOf(24936L, 61812L)), events.map { event -> RunningSessionsAnswer.parse(event.data).runningSessions.map { it.processId } })
    }
}
