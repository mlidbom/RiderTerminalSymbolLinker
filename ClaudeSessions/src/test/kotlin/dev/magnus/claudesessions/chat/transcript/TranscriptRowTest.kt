package dev.magnus.claudesessions.chat.transcript

import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolInputField
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolResult
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallRun
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/**
 * Which rows the transcript shows of a conversation: tool calls Claude made one after the other as one line that counts
 * them, which opens to theirs, and a call by itself as its own line.
 */
class TranscriptRowTest {
    private val prompt = Prompt("p1", "Look around")
    private val listing = call("t1", "Bash", "dir", ToolResult("a.txt", isAnError = false))
    private val reading = call("t2", "Read", "a.txt", ToolResult("hello", isAnError = false))
    private val grepping = call("t3", "Grep", "hello", ToolResult("no matches", isAnError = true))
    private val testing = call("t4", "PowerShell", "dotnet test", result = null)
    private val answer = AssistantText("a1", "Done.")

    @Test
    fun `a call by itself is its own row, and calls one after the other are one row that counts them`() {
        val rows = TranscriptRow.of(listOf(prompt, listing, answer, reading, grepping, testing), opened = emptySet())

        assertEquals(listOf("p1", "t1", "a1", "tool calls from t2"), rows.map { it.key })
        assertEquals("Read 1 file, searched 1 time, ran 1 command · 1 failed · running", (rows.last() as TranscriptRow.OfAToolCallRun).run.summary)
    }

    @Test
    fun `an open run shows its calls below it, each open as the user opened it`() {
        val rows = TranscriptRow.of(listOf(reading, grepping, testing), opened = setOf("tool calls from t2", "t3"))

        assertEquals(
            listOf("tool calls from t2" to true, "t2" to false, "t3" to true, "t4" to false),
            rows.map { row -> row.key to ((row as? TranscriptRow.OfAToolCall)?.isOpen ?: (row as TranscriptRow.OfAToolCallRun).isOpen) },
        )
        assertEquals(listOf(false, true, true, true), rows.map { it is TranscriptRow.OfAToolCall && it.inARun })
    }

    @Test
    fun `a prompt from elsewhere is a row of its own, open as the user opened it`() {
        val message = PromptFromElsewhere("m1", PromptFromElsewhere.Source.AnotherSession("tooling-owner"), "Published.")

        assertEquals(
            listOf(TranscriptRow.OfAnItem(prompt), TranscriptRow.OfAPromptFromElsewhere(message, isOpen = true)),
            TranscriptRow.of(listOf(prompt, message), opened = setOf("m1")),
        )
    }

    @Test
    fun `a run counts each kind of call once, in the order they first came`() {
        val run = ToolCallRun(listOf(listing, call("t5", "Bash", "dir b", ToolResult("", isAnError = false)), reading, call("t6", "mcp__idea__read", "x", ToolResult("", isAnError = false))))

        assertEquals("Ran 2 commands, read 1 file, made 1 other tool call", run.summary)
    }

    private fun call(id: String, tool: String, input: String, result: ToolResult?) = ToolCall(id, tool, listOf(ToolInputField("input", input)), result)
}
