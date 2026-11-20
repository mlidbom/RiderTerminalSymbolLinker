package dev.magnus.claudesessions.hub

import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Interruption
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import dev.magnus.claudesessions.chat.claudecode.conversation.Thinking
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolInputField
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolResult
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/**
 * Whether the conversation read from the hub's stream has only `\n` for its line separators, as the IDE's documents take:
 * on 2026-11-20, the first deploy to Magnus's IDEs showed an empty chat, since a tool's result had `\r\n`, and the
 * transcript's document refused it.
 */
class TheHubsConversationStreamTest {
    @Test
    fun `every text of the conversation has its line separators as the IDE's documents take them`() {
        val stream = TheHubsConversationStream()
        val events = listOf(
            message("""{"id": "1", "sessionId": "s", "kind": "promptOfMagnuss", "parts": [{"kind": "typed", "text": "Typed\r\nand pasted\r"}]}"""),
            message("""{"id": "2", "sessionId": "s", "kind": "promptFromElsewhere", "from": "program", "text": "From\r\na program"}"""),
            message("""{"id": "3", "sessionId": "s", "kind": "thinking", "text": "Thinking\r\nit over"}"""),
            message("""{"id": "4", "sessionId": "s", "kind": "claudesText", "text": "Claude\r\nsays"}"""),
            message("""{"id": "5", "sessionId": "s", "kind": "toolCall", "name": "PowerShell", "toolUseId": "t", "input": {"command": "Get-Date\r\nGet-Location"}}"""),
            ServerSentEvent("tool-result", """{"toolUseId": "t", "text": "Thursday\r\nC:\\Dev", "isError": false}"""),
            message("""{"id": "6", "sessionId": "s", "kind": "slashCommand", "asTyped": "/compact keep\r\nthis"}"""),
            message("""{"id": "7", "sessionId": "s", "kind": "interruption", "text": "Interrupted\r\nhere"}"""),
            ServerSentEvent("caught-up", "{}"),
        )

        val told = events.mapNotNull(stream::read).single()

        assertEquals(
            listOf(
                Prompt("1", "Typed\nand pasted\n"),
                PromptFromElsewhere("2", PromptFromElsewhere.Source.Program, "From\na program"),
                Thinking("3", "Thinking\nit over"),
                AssistantText("4", "Claude\nsays"),
                ToolCall("5", "PowerShell", listOf(ToolInputField("command", "Get-Date\nGet-Location")), ToolResult("Thursday\nC:\\Dev", isAnError = false)),
                Prompt("6", "/compact keep\nthis"),
                Interruption("7", "Interrupted\nhere"),
            ),
            told.conversation.items,
        )
    }

    private fun message(data: String) = ServerSentEvent("message", data)
}
