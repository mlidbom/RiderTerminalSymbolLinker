package dev.magnus.claudesessions.chat.transcript

import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall

/**
 * The one line the [TranscriptPane] shows for a [ToolCall] while its input and result are folded away: the tool's name,
 * and what it was run on, such as `Bash: git status` or `Read: C:\Dev\Some\File.cs`.
 */
object ToolCallHeader {
    /** [call]'s header. */
    fun of(call: ToolCall): String {
        val whatItRanOn = (FIELD_THAT_SAYS_WHAT_IT_RAN_ON[call.tool]?.let(call::inputField) ?: call.input.firstOrNull())?.value?.lineSequence()?.firstOrNull()
        return if (whatItRanOn.isNullOrBlank()) call.tool else "${call.tool}: ${shortened(whatItRanOn)}"
    }

    private fun shortened(line: String): String = if (line.length <= LONGEST) line else line.take(LONGEST) + "…"

    /** The longest the part after the tool's name is shown: a whole long command is in the fold. */
    private const val LONGEST = 200

    /** For each of Claude Code's own tools, the field of its input that says best what it ran on. Others show their first. */
    private val FIELD_THAT_SAYS_WHAT_IT_RAN_ON = mapOf(
        "Bash" to "command",
        "PowerShell" to "command",
        "Read" to "file_path",
        "Write" to "file_path",
        "Edit" to "file_path",
        "NotebookEdit" to "notebook_path",
        "Grep" to "pattern",
        "Glob" to "pattern",
        "Agent" to "description",
        "Task" to "description",
        "WebFetch" to "url",
        "WebSearch" to "query",
        "Skill" to "skill",
    )
}
