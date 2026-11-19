package dev.magnus.claudesessions.hub

import com.google.gson.JsonArray
import com.google.gson.JsonObject
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.PastedImage
import dev.magnus.claudesessions.chat.claudecode.PastedText
import dev.magnus.claudesessions.chat.claudecode.TypedText
import dev.magnus.claudesessions.claudecode.ClaudeCodeCommand
import dev.magnus.claudesessions.claudecode.RunningClaudeCode
import java.time.Duration

/**
 * Typing into Claude Code, and into the shell below it, through the [ClaudeToolingHub], which owns all such typing, as
 * agreed (`~/.claude/dev_docs/the-claude-tooling-bundle.md`): it waits for its turn at typing into a session, so that it
 * never interleaves with the tooling's own typing, such as a `/clear`, and types as Claude Code needs it typed.
 *
 * Each call is a request of the hub's local API, answered once typed, or with why not: an answer may take up to a minute
 * while the tooling's typing has the session's turn. The contract is `~/.claude/dev_docs/the-hubs-local-api.md`, "Typing into
 * a running session".
 */
class TypingIntoClaudeCode internal constructor(private val requests: RequestsToTheHub) {
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
     * Posts [body], or none, to [path] of the hub's API, and returns the hub's answer once it has typed, from whichever hub
     * answers it, as [RequestsToTheHub] says.
     */
    private suspend fun type(path: String, body: JsonObject?): TypingAnswer =
        when (val answer = requests.post(path, body, TYPING_MAY_TAKE)) {
            is HubAnswer.Answered -> TypingAnswer.of(answer.status, answer.body, answer.build)
            is HubAnswer.NoHubAnswered -> TypingAnswer.NotTyped(answer.why, reason = null)
        }

    companion object {
        /**
         * The hub waits up to a minute for its turn at typing into a session, and refuses then; typing a long message,
         * and seeing it sent, takes seconds more.
         */
        private val TYPING_MAY_TAKE: Duration = Duration.ofMinutes(2)

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
