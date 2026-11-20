package dev.magnus.claudesessions.hub

import com.google.gson.JsonParser
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.PastedImage
import dev.magnus.claudesessions.chat.claudecode.PastedText
import dev.magnus.claudesessions.chat.claudecode.TypedText
import dev.magnus.claudesessions.chat.claudecode.TypingOutcome
import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Clearing
import dev.magnus.claudesessions.chat.claudecode.conversation.Compaction
import dev.magnus.claudesessions.chat.claudecode.conversation.Interruption
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import dev.magnus.claudesessions.chat.claudecode.conversation.Thinking
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolInputField
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolResult
import dev.magnus.claudesessions.claudecode.ClaudeCodeCommand
import dev.magnus.claudesessions.claudecode.ClaudeCodesStatusLine
import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.claudecode.ClaudeSessionId
import dev.magnus.claudesessions.claudecode.TurnUnderWay
import dev.magnus.claudesessions.sessionhosts.SessionHostId
import dev.magnus.claudesessions.sessionhosts.SessionHostLabel
import java.net.URI
import java.nio.file.Path
import java.time.Instant
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.assertThrows

/**
 * Whether the plugin reads the hub's local API as the hub answers it, and asks as the hub reads it: on copies of the
 * samples of its answers that the hub's own specifications compare its answers with, in
 * `~/.claude/src/ClaudeTooling.Specifications/Hub/LocalApi/Samples`, copied on 2026-11-19 from the tooling's
 * commits cc7853b, for typing 1d16ff3, and for session hosts eff80d7, and message-sent.http and the conversation's,
 * conversation-stream.http and no-such-session.http, from 7b23e8a, and still the same on 2026-11-20, the running
 * sessions' with their status lines and turns, and the turn's stream, from 2dae3f0, and on the
 * examples of its contract,
 * `~/.claude/dev_docs/the-hubs-local-api.md`.
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
    fun `the running sessions are read with each one's Claude Code's process, its session, its status, its name, its directory, its session host, its status line and its turn under way`() {
        val answer = HttpSample(HttpSample.named("running-sessions.http"))

        assertEquals(200, answer.status)
        assertEquals(HubProtocol.VERSION.toString(), answer.header(HubProtocol.HEADER))
        assertEquals(
            listOf(
                RunningSessionInTheAnswer(24936, "87c58397-ae2b-4346-ab49-ebc624ed1193", "waiting", "chat-view", "C:\\Dev\\ClaudeSymbolLinker", "3f9a0c71d2e84b56", OPUS_IN_BYPASS, TURN_RUNNING_TOOLS),
                RunningSessionInTheAnswer(61812, "34aba8f2-a171-443f-955b-d2733a92adf2", "idle", null, "C:\\Users\\magnu\\.claude", null, ClaudeCodesStatusLine("Haiku 4.5", null, null, null), null),
                RunningSessionInTheAnswer(70364, "ac61d91a-65b8-4bb0-85d4-d492f9fd0b45", "busy", "throwaway-session", "C:\\Dev\\Deskmancer", null, null, TURN_WRITING_A_WRITE_CALL),
            ),
            RunningSessionsAnswer.parse(answer.body).runningSessions,
        )
    }

    @Test
    fun `a status line says the model, the effort level, the permission mode in short and the context used, leaving out what isn't known`() {
        assertEquals("Opus 5.5 · xhigh · Bypass permissions · context 31%", OPUS_IN_BYPASS.text)
        assertEquals("Haiku 4.5", ClaudeCodesStatusLine("Haiku 4.5", null, null, null).text)
        assertEquals("Opus 5.5 · Plan", ClaudeCodesStatusLine("Opus 5.5", null, null, ClaudeCodesStatusLine.PermissionMode("plan", "plan mode on")).text)
        assertEquals("Opus 5.5 · some new mode on", ClaudeCodesStatusLine("Opus 5.5", null, null, ClaudeCodesStatusLine.PermissionMode(null, "some new mode on")).text)
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
    fun `typed into a session, a message sent, queued or not, or Claude Code started in a shell, is typed`() {
        assertEquals(TypingAnswer.Typed, typingAnswerIn("typed-into-the-session.http"))
        assertEquals(TypingAnswer.Typed, typingAnswerIn("message-sent.http"))
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
    fun `the chat is told what came of what it typed, and whether the terminal shows why`() {
        val outcomes = mapOf(
            "typed-into-the-session.http" to TypingOutcome.Typed::class,
            "input-box-holds-text.http" to TypingOutcome.NothingTyped::class,
            "no-input-box.http" to TypingOutcome.NothingTyped::class,
            "no-such-claude-code.http" to TypingOutcome.NothingTyped::class,
            "nothing-to-interrupt.http" to TypingOutcome.NothingTyped::class,
            "turn-at-typing-not-free.http" to TypingOutcome.NothingTyped::class,
            "input-box-shows-other-than-typed.http" to TypingOutcome.LeftUnsentInTheTerminal::class,
            "not-sent-by-enter.http" to TypingOutcome.LeftUnsentInTheTerminal::class,
        )
        val theTerminalShowsWhy = setOf("input-box-holds-text.http", "no-input-box.http", "input-box-shows-other-than-typed.http", "not-sent-by-enter.http")

        outcomes.forEach { (sample, kind) ->
            val outcome = TheHubBehindTheChat.outcomeOf(typingAnswerIn(sample))
            assertEquals(kind, outcome::class, sample)
            assertEquals(sample in theTerminalShowsWhy, outcome.theTerminalShowsWhy, sample)
        }
        assertEquals((typingAnswerIn("input-box-holds-text.http") as TypingAnswer.NotTyped).why, TheHubBehindTheChat.outcomeOf(typingAnswerIn("input-box-holds-text.http")).whyNotTyped)
    }

    @Test
    fun `the session hosts are read with each one's id, its label, whether it's attached, and the command that attaches to it`() {
        val answer = HttpSample(HttpSample.named("session-hosts.http"))
        val hosts = SessionHosts.hostsIn(answer.body)

        assertEquals(200, answer.status)
        assertEquals(listOf("3f9a0c71d2e84b56", "b0e41d9a6c2f8835"), hosts.map { it.id.value })
        assertEquals(listOf(true, false), hosts.map { it.attached })
        assertEquals(listOf("C:\\Users\\magnu\\.claude\\bin\\ClaudeTooling.SessionHost.exe", "attach", "3f9a0c71d2e84b56"), hosts[0].attachCommand)
        assertEquals(SessionHostLabel(Path.of("C:\\Users\\magnu\\AppData\\Roaming\\JetBrains\\Rider2026.3"), Path.of("C:\\Dev\\ClaudeSymbolLinker")), SessionHostLabel.of(hosts[0].label))
        assertEquals(null, SessionHostLabel.of(hosts[1].label))
    }

    @Test
    fun `a session host started is read as the hosts are listed`() {
        val answer = HttpSample(HttpSample.named("session-host-started.http"))

        assertEquals(201, answer.status)
        assertEquals(SessionHostId("3f9a0c71d2e84b56"), SessionHosts.hostIn(JsonParser.parseString(answer.body).asJsonObject.getAsJsonObject("sessionHost")).id)
    }

    @Test
    fun `the label the plugin gives a session host reads back as it was given`() {
        val label = SessionHostLabel(Path.of("C:\\Users\\magnu\\AppData\\Roaming\\JetBrains\\Rider2026.3"), Path.of("C:\\Dev\\Magnus's {project}"))

        assertEquals(label, SessionHostLabel.of(label.text))
        assertEquals(null, SessionHostLabel.of("not JSON {"))
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

    @Test
    fun `a conversation's stream is read into each kind of item the chat shows, as of each caught-up`() {
        val stream = HttpSample(HttpSample.named("conversation-stream.http"))
        val reading = ServerSentEvents()
        val conversation = TheHubsConversationStream()
        val told = stream.bodyLines.mapNotNull(reading::read).mapNotNull(conversation::read)

        assertEquals(200, stream.status)
        assertEquals(2, told.size)
        assertEquals(
            listOf(
                Prompt("7bfc8827-e689-57f9-ad85-0475d64b2557", "/clear"),
                Clearing("51f73c28-aa76-5c10-bdbd-47a25730b162", goesOnIn = "87c58397-ae2b-4346-ab49-ebc624ed1193"),
                Prompt("aa02714e-1a9c-5920-a408-da077fa9fd90", "Fix the merge, please.\n\nthe merge's log"),
                Thinking("e2a48d99-2a3b-5e4e-a10e-0c703c6864cf", "Reading the merge first."),
                AssistantText("bcb000ca-83d2-59fc-bb1b-ae470327b85d", "I'll look at **the merge**."),
                ToolCall(
                    "be1335c1-4197-5192-96e7-9e8bd8015e85", "Bash",
                    listOf(ToolInputField("command", "git status"), ToolInputField("description", "Show the status")),
                    ToolResult("M file.cs", isAnError = false),
                ),
                PromptFromElsewhere("01426427-3fa4-52a5-9352-98b541b69b4d", PromptFromElsewhere.Source.AnotherSession("deskmancer"), "Can you look at this?"),
                Prompt("19347bec-e330-5e3d-b6bb-06b858943fa5", "Look: [Image #1]"),
                Interruption("cd9ad7a2-a1b3-58fe-9ede-06c644fd70c3", "[Request interrupted by user]"),
                Prompt("467c3f8b-073f-56c6-bd5c-d04124ccc218", "/compact keep the fix"),
                Compaction("ace6d5fe-ad40-5a30-b04a-ca1a134509e8"),
                AssistantText("6b2f146d-31a4-58d9-95c9-9ee9fbbec331", "Fixed."),
            ),
            told[0].conversation.items,
        )
        assertEquals(setOf(ClaudeSessionId("87c58397-ae2b-4346-ab49-ebc624ed1193")), told[0].sessions)
    }

    @Test
    fun `a rewind takes away what came after the message it keeps, and the new branch follows`() {
        val stream = HttpSample(HttpSample.named("conversation-stream.http"))
        val reading = ServerSentEvents()
        val conversation = TheHubsConversationStream()
        val (before, after) = stream.bodyLines.mapNotNull(reading::read).mapNotNull(conversation::read)

        assertEquals(before.conversation.items.dropLast(1), after.conversation.items.dropLast(1))
        assertEquals(Prompt("50f1a89b-eb80-5c05-b2fa-387763846f2e", "Now push it."), after.conversation.items.last())
    }

    @Test
    fun `a session the hub knows nothing of is no such session`() {
        val sample = HttpSample(HttpSample.named("no-such-session.http"))

        assertEquals(404, sample.status)
        assertEquals("noSuchSession", JsonParser.parseString(sample.body).asJsonObject.get("reason").asString)
    }

    private fun typingAnswerIn(sample: String): TypingAnswer =
        HttpSample(HttpSample.named(sample)).let { TypingAnswer.of(it.status, it.body, build = "2026-11-19T20:38:00Z") }

    private companion object {
        val OPUS_IN_BYPASS = ClaudeCodesStatusLine("Opus 5.5", "xhigh", 31, ClaudeCodesStatusLine.PermissionMode("bypassPermissions", "bypass permissions on"))
        val TURN_RUNNING_TOOLS = TurnUnderWay(Instant.parse("2026-11-19T18:03:58.214Z"), TurnUnderWay.Activity.RUNNING_TOOLS, toolBeingWritten = null, outputTokensSoFar = 1228)
        val TURN_WRITING_A_WRITE_CALL = TurnUnderWay(Instant.parse("2026-11-19T18:05:01.071Z"), TurnUnderWay.Activity.WRITING_A_TOOL_CALL, toolBeingWritten = "Write", outputTokensSoFar = 2044)
    }
}
