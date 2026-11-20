package dev.magnus.claudesessions.hub

import com.google.gson.JsonElement
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Clearing
import dev.magnus.claudesessions.chat.claudecode.conversation.Compaction
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.chat.claudecode.conversation.ConversationItem
import dev.magnus.claudesessions.chat.claudecode.conversation.Interruption
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import dev.magnus.claudesessions.chat.claudecode.conversation.Thinking
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolInputField
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolResult
import dev.magnus.claudesessions.claudecode.ClaudeSessionId

/**
 * The hub's stream of a session's conversation, `GET /sessions/{id}/conversation/stream`, read event by event into the
 * [ConversationOfALine] it tells of, as its contract says (`~/.claude/dev_docs/the-hubs-local-api.md`, "A session's
 * conversation"): each `message` added at the end, each `tool-result` given to its tool call, and each `rewind` taking
 * away the messages after the one it keeps. At each `caught-up`, the conversation as it is then.
 *
 * A message of a kind this build doesn't know is left out, as the contract says kinds may be added.
 *
 * Every text of the conversation it reads has `\n` for its line separators, as the IDE's documents take only that: a
 * text of Claude Code's is as it was written, and on Windows a tool's result, or a pasted prompt, has `\r\n`.
 */
class TheHubsConversationStream {
    /** The messages so far, in their order, each as the hub sent it. */
    private val messages = mutableListOf<JsonObject>()

    /** The results of the tool calls so far, by their tool calls' ids. */
    private val results = mutableMapOf<String, ToolResult>()

    /** Reads [event], and returns the conversation as it is, when the event says the stream has caught up. */
    fun read(event: ServerSentEvent): ConversationOfALine? {
        when (event.name) {
            "message" -> messages += JsonParser.parseString(event.data).asJsonObject
            "tool-result" -> JsonParser.parseString(event.data).asJsonObject.let { results[it.get("toolUseId").asString] = ToolResult(it.get("text").asText, it.get("isError").asBoolean) }
            "rewind" -> rewindTo(JsonParser.parseString(event.data).asJsonObject.get("lastMessageKept").stringOrNull())
            "caught-up" -> return ConversationOfALine(Conversation(messages.mapNotNull(::itemOf)), messages.map { ClaudeSessionId(it.get("sessionId").asString) }.toSet())
        }
        return null
    }

    /** Takes away the messages after [lastMessageKept], or every message when it's null. */
    private fun rewindTo(lastMessageKept: String?) {
        val kept = if (lastMessageKept == null) 0 else messages.indexOfFirst { it.get("id").asString == lastMessageKept } + 1
        check(lastMessageKept == null || kept > 0) { "The hub rewound the conversation to the message $lastMessageKept, which it didn't send." }
        messages.subList(kept, messages.size).clear()
    }

    /** The item of the conversation [message] is, or null when it's of a kind this build doesn't know. */
    private fun itemOf(message: JsonObject): ConversationItem? {
        val id = message.get("id").asString
        return when (message.get("kind").asString) {
            "promptOfMagnuss" -> Prompt(id, textOfThePrompt(message))
            "promptFromElsewhere" -> sourceOf(message)?.let { PromptFromElsewhere(id, it, message.get("text").asText) }
            "claudesText" -> AssistantText(id, message.get("text").asText)
            "thinking" -> Thinking(id, message.get("text").asText)
            "toolCall" -> ToolCall(id, message.get("name").asString, inputOf(message.getAsJsonObject("input")), results[message.get("toolUseId").asString])
            "slashCommand" -> Prompt(id, message.get("asTyped").asText)
            "interruption" -> Interruption(id, message.get("text").asText)
            "compaction" -> Compaction(id)
            "clearing" -> Clearing(id, message.get("goesOnIn").asString)
            else -> null
        }
    }

    /**
     * A prompt's text as the chat's input box would have held it: its typed and pasted parts' texts, and each image as
     * its place-holder, numbered in the prompt, as the box numbers them: `[Image #1]`.
     */
    private fun textOfThePrompt(message: JsonObject): String {
        var images = 0
        return message.getAsJsonArray("parts").joinToString("") { element ->
            val part = element.asJsonObject
            if (part.get("kind").asString == "pastedImage") "[Image #${++images}]" else part.get("text")?.asText.orEmpty()
        }
    }

    /** Where the prompt [message] came from, or null when from where this build doesn't know, as one of a kind added since. */
    private fun sourceOf(message: JsonObject): PromptFromElsewhere.Source? = when (message.get("from").asString) {
        "anotherSession" -> PromptFromElsewhere.Source.AnotherSession(message.get("sendingSessionName").stringOrNull())
        "backgroundTask" -> PromptFromElsewhere.Source.BackgroundTask
        "scheduledWakeUp" -> PromptFromElsewhere.Source.ScheduledWakeUp
        "claudeCode" -> PromptFromElsewhere.Source.ClaudeCode
        "program" -> PromptFromElsewhere.Source.Program
        else -> null
    }

    /** A tool's input, field by field: a text as it is, anything else as its JSON. */
    private fun inputOf(input: JsonObject): List<ToolInputField> =
        input.entrySet().map { (name, value) -> ToolInputField(name, if (value.isJsonPrimitive && value.asJsonPrimitive.isString) value.asText else value.toString()) }

    private fun JsonElement?.stringOrNull(): String? = this?.takeUnless { it.isJsonNull }?.asString

    /** A text of the conversation, with `\n` for every line separator, `\r\n` and a lone `\r` alike. */
    private val JsonElement.asText: String get() = asString.replace("\r\n", "\n").replace('\r', '\n')
}
