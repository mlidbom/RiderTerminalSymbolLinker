package dev.magnus.claudesessions.hub

import com.google.gson.JsonParser
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.PastedImage
import dev.magnus.claudesessions.chat.claudecode.PastedText
import dev.magnus.claudesessions.chat.claudecode.TypedText
import dev.magnus.claudesessions.claudecode.ClaudeCodeCommand
import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.claudecode.ClaudeSessionId
import java.net.URI
import java.nio.file.Path
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.assertThrows

/**
 * Whether the plugin reads the hub's local API as the hub answers it, and asks as the hub reads it: on copies of the
 * samples of its answers that the hub's own specifications compare its answers with, in
 * `~/.claude/src/ClaudeTooling.Specifications/ResidentServer/LocalApi/Samples`, copied on 2026-11-19 from the tooling's
 * commits cc7853b and, for typing, 1d16ff3, and on the examples of its contract, `~/.claude/dev_docs/the-hubs-local-api.md`.
 * When the samples change, as the contract changes, copy them again.
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

    @Test
    fun `typed into a session, or Claude Code started in a shell, is typed`() {
        assertEquals(TypingAnswer.Typed, typingAnswerIn("typed-into-the-session.http"))
        assertEquals(TypingAnswer.Typed, typingAnswerIn("claude-code-started-in-the-shell.http"))
    }

    @Test
    fun `each refusal is not typed, with the hub's reason, and why for the user`() {
        val refusals = mapOf(
            "input-box-holds-text.http" to "inputBoxHoldsText",
            "input-box-shows-other-than-typed.http" to "inputBoxShowsOtherThanTyped",
            "no-input-box.http" to "noInputBox",
            "no-such-claude-code.http" to "noSuchClaudeCode",
            "no-such-shell.http" to "noSuchShell",
            "not-sent-by-enter.http" to "notSentByEnter",
            "nothing-to-interrupt.http" to "nothingToInterrupt",
            "shell-not-known.http" to "shellNotKnown",
            "shell-runs-a-program.http" to "shellRunsAProgram",
            "turn-at-typing-not-free.http" to "turnAtTypingNotFree",
        )

        refusals.forEach { (sample, reason) -> assertEquals(reason, (typingAnswerIn(sample) as TypingAnswer.NotTyped).reason, sample) }
        assertEquals(
            TypingAnswer.NotTyped("The shell runs claude.exe, process 113568, which would get the line: nothing typed.", "shellRunsAProgram"),
            typingAnswerIn("shell-runs-a-program.http"),
        )
    }

    @Test
    fun `a request the hub can't read is a bug of the plugin's, not an answer`() {
        assertThrows<IllegalStateException> { typingAnswerIn("bad-request.http") }
    }

    @Test
    fun `a message's parts are sent in order, each of its kind, an image by its file's full path`() {
        val message = Message(listOf(TypedText("Look at this:\nthe second line, "), PastedText("pasted\nlines"), PastedImage(Path.of("C:\\Users\\magnu\\AppData\\Local\\Temp\\red square.png"), "[Image #1]")))

        assertEquals(
            JsonParser.parseString(
                """
                {
                  "parts": [
                    {"kind": "typedText", "text": "Look at this:\nthe second line, "},
                    {"kind": "pastedText", "text": "pasted\nlines"},
                    {"kind": "pastedImage", "file": "C:\\Users\\magnu\\AppData\\Local\\Temp\\red square.png"}
                  ]
                }
                """,
            ),
            TypingIntoClaudeCode.bodyOf(message),
        )
    }

    @Test
    fun `Claude Code is started as a new session, a named one, or resuming a session`() {
        val session = ClaudeSession(ClaudeSessionId("1d24f54b-3002-4132-8a65-4d7dc8977525"), Path.of("C:\\Dev"), name = null)

        assertEquals(JsonParser.parseString("{}"), TypingIntoClaudeCode.bodyThatRuns(ClaudeCodeCommand.StartingANewSession(name = null)))
        assertEquals(JsonParser.parseString("""{"name": "Fixing the login bug"}"""), TypingIntoClaudeCode.bodyThatRuns(ClaudeCodeCommand.StartingANewSession("Fixing the login bug")))
        assertEquals(JsonParser.parseString("""{"sessionToResume": "1d24f54b-3002-4132-8a65-4d7dc8977525"}"""), TypingIntoClaudeCode.bodyThatRuns(ClaudeCodeCommand.Resuming(session)))
    }

    private fun typingAnswerIn(sample: String): TypingAnswer =
        HttpSample(HttpSample.named(sample)).let { TypingAnswer.of(it.status, it.body, build = "2026-11-19T20:38:00Z") }
}
