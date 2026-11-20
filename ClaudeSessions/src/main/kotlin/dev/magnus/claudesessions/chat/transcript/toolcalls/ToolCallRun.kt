package dev.magnus.claudesessions.chat.transcript.toolcalls

import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/**
 * Tool calls Claude made one after the other, with nothing else in the conversation between them: the [TranscriptPane]
 * shows a run of two or more as one line, its [summary], such as `Ran 2 commands, read 1 file`, with the calls below it
 * once the user opens it.
 */
data class ToolCallRun(val calls: List<ToolCall>) {
    /**
     * What tells the run from every other row of the transcript, the same as long as it starts with the same call: as it
     * grows, as Claude makes more calls, it stays open if the user opened it.
     */
    val key: String get() = "tool calls from ${calls.first().id}"

    /** What the calls did, counted by kind, as `Ran 2 commands, read 1 file`, and whether the last still runs, or any failed. */
    val summary: String
        get() {
            val counts = calls.groupingBy { KIND_OF[it.tool] ?: OTHER }.eachCount()
            val said = counts.entries.joinToString(", ") { (kind, count) -> kind.said(count) }.replaceFirstChar(Char::uppercase)
            return said + ToolCallStatus.of(calls).suffix
        }

    /** A kind of tool call, as the summary counts them: what it says of [count] calls of the kind. */
    private class Kind(private val verb: String, private val one: String, private val many: String) {
        fun said(count: Int): String = "$verb $count ${if (count == 1) one else many}"
    }

    private companion object {
        val COMMANDS = Kind("ran", "command", "commands")
        val EDITS = Kind("made", "edit", "edits")
        val SEARCHES = Kind("searched", "time", "times")
        val AGENTS = Kind("ran", "agent", "agents")
        val OTHER = Kind("made", "other tool call", "other tool calls")

        /** The kinds of Claude Code's own tools. Others, such as MCP tools, are counted together. */
        val KIND_OF = mapOf(
            "Bash" to COMMANDS,
            "PowerShell" to COMMANDS,
            "Read" to Kind("read", "file", "files"),
            "Write" to Kind("wrote", "file", "files"),
            "Edit" to EDITS,
            "MultiEdit" to EDITS,
            "NotebookEdit" to EDITS,
            "Grep" to SEARCHES,
            "Glob" to SEARCHES,
            "WebFetch" to Kind("fetched", "page", "pages"),
            "WebSearch" to Kind("searched the web", "time", "times"),
            "Agent" to AGENTS,
            "Task" to AGENTS,
            "Skill" to Kind("used", "skill", "skills"),
        )
    }
}
