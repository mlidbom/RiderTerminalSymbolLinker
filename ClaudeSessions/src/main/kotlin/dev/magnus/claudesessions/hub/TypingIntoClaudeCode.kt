package dev.magnus.claudesessions.hub

import com.google.gson.JsonArray
import com.google.gson.JsonObject
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.PastedImage
import dev.magnus.claudesessions.chat.claudecode.PastedText
import dev.magnus.claudesessions.chat.claudecode.TypedText
import dev.magnus.claudesessions.claudecode.ClaudeCodeCommand
import dev.magnus.claudesessions.claudecode.ClaudeCodeFolder
import dev.magnus.claudesessions.claudecode.RunningClaudeCode
import java.net.ConnectException
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.time.Duration
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runInterruptible

/**
 * Typing into Claude Code, and into the shell below it, through the [ClaudeToolingHub], which owns all such typing, as
 * agreed (`~/.claude/dev_docs/the-claude-tooling-bundle.md`): it waits for its turn at typing into a session, so that it
 * never interleaves with the tooling's own typing, such as a `/clear`, and types as Claude Code needs it typed.
 *
 * Each call is a request of the hub's local API, answered once typed, or with why not: an answer may take minutes, while
 * the tooling's typing has the session's turn. The contract is `~/.claude/dev_docs/the-hubs-local-api.md`, "Typing into
 * a running session".
 */
class TypingIntoClaudeCode internal constructor(private val client: HttpClient) {
    /**
     * Types the line that runs [command] into [shell], quoted as that shell reads it, and Enter after it: for a shell
     * that has started, or whose Claude Code has exited. What's typed before the shell reads its command line waits in its
     * console's input.
     */
    suspend fun startClaudeCode(shell: ProcessHandle, command: ClaudeCodeCommand): TypingAnswer =
        type("/shells/${shell.pid()}/start-claude-code", bodyThatRuns(command))

    /**
     * Gives [claudeCode] [message], as if typed and pasted into its input box, and presses Enter: refused while its box
     * holds text of the user's own, or while it shows no box, as while a dialog waits for the user.
     */
    suspend fun send(claudeCode: RunningClaudeCode, message: Message): TypingAnswer =
        type("/running-sessions/${claudeCode.process.pid()}/send", bodyOf(message))

    /** Presses Escape in [claudeCode]'s console, which interrupts its turn, or cancels the dialog it shows. */
    suspend fun interrupt(claudeCode: RunningClaudeCode): TypingAnswer =
        type("/running-sessions/${claudeCode.process.pid()}/interrupt", body = null)

    /** Presses Shift+Tab in [claudeCode]'s console, which changes its permission mode to the next. */
    suspend fun changeThePermissionMode(claudeCode: RunningClaudeCode): TypingAnswer =
        type("/running-sessions/${claudeCode.process.pid()}/change-the-permission-mode", body = null)

    /**
     * Posts [body], or none, to [path] of the hub's API, and returns the hub's answer once it has typed. At a publish of a
     * new build, the hub asked stops: it refuses the connection, or answers 503 while the request waits, or another hub,
     * which took its port, answers 401. So the request goes to the hub the announcement names then, as the contract
     * says, until one answers or [LOOKING_FOR_A_HUB_MAY_TAKE] has passed. When none is announced, the published hub is
     * started, once: see [ThePublishedHub].
     */
    private suspend fun type(path: String, body: JsonObject?): TypingAnswer = runInterruptible(Dispatchers.IO) { typeLookingForAHub(path, body) }

    /** [type], on a thread it blocks, and which an interrupt ends. */
    private fun typeLookingForAHub(path: String, body: JsonObject?): TypingAnswer {
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
                    return TypingAnswer.NotTyped("The Claude tooling's hub speaks protocol ${hub.protocol}, which this build of Claude Sessions doesn't know: restart the IDE.", reason = null)
                !hub.serves(ClaudeCodeFolder.path) ->
                    return TypingAnswer.NotTyped("The Claude tooling's hub serves the Claude Code folder ${hub.claudeCodeFolder}, and this IDE runs Claude Code with ${ClaudeCodeFolder.path}.", reason = null)
                else -> answerOf(hub, path, body)?.let { return it }
            }
            if (System.nanoTime() > deadline) return TypingAnswer.NotTyped("No Claude tooling hub answered within ${LOOKING_FOR_A_HUB_MAY_TAKE.toSeconds()} seconds.", reason = null)
            Thread.sleep(BETWEEN_TRIES.toMillis())
        }
    }

    /** The answer of [hub] to the request, or null when [hub] is no longer there to answer it, as at a publish. */
    private fun answerOf(hub: HubAnnouncement, path: String, body: JsonObject?): TypingAnswer? {
        val request = hub.request(path).timeout(TYPING_MAY_TAKE).header("Content-Type", "application/json")
            .POST(body?.let { HttpRequest.BodyPublishers.ofString(it.toString()) } ?: HttpRequest.BodyPublishers.noBody()).build()
        val answer = try {
            client.send(request, HttpResponse.BodyHandlers.ofString())
        } catch (_: ConnectException) {
            return null
        }
        if (answer.statusCode() == 401 || answer.statusCode() == 503) return null
        return TypingAnswer.of(answer.statusCode(), answer.body(), hub.build)
    }

    companion object {
        /** The hub waits up to 10 minutes for its turn at typing into a session, and refuses then: a minute more. */
        private val TYPING_MAY_TAKE: Duration = Duration.ofMinutes(11)

        /** How long a request looks for a hub to answer it: one starting, or the next build's at a publish. */
        private val LOOKING_FOR_A_HUB_MAY_TAKE: Duration = Duration.ofSeconds(30)

        private val BETWEEN_TRIES: Duration = Duration.ofMillis(250)

        /** The body of `start-claude-code` that runs [command]: `{}`, `{"name": …}` or `{"sessionToResume": …}`. */
        internal fun bodyThatRuns(command: ClaudeCodeCommand): JsonObject = JsonObject().apply {
            when (command) {
                is ClaudeCodeCommand.StartingANewSession -> command.name?.let { addProperty("name", it) }
                is ClaudeCodeCommand.Resuming -> addProperty("sessionToResume", command.session.id.value)
            }
        }

        /** The body of `send`: [message]'s parts, in order, each of the kind the hub types it as. */
        internal fun bodyOf(message: Message): JsonObject = JsonObject().apply {
            add("parts", JsonArray().apply {
                message.parts.forEach { part ->
                    add(JsonObject().apply {
                        when (part) {
                            is TypedText -> addProperty("kind", "typedText").also { addProperty("text", part.text) }
                            is PastedText -> addProperty("kind", "pastedText").also { addProperty("text", part.text) }
                            is PastedImage -> addProperty("kind", "pastedImage").also { addProperty("file", part.file.toAbsolutePath().toString()) }
                        }
                    })
                }
            })
        }
    }
}
