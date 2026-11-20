package dev.magnus.claudesessions.chat.transcript.toolcalls

import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/**
 * What the [TranscriptPane] shows below a [ToolCall]'s header once the user opens it: what Claude gave the tool, field
 * by field, and what the tool gave back, or that it still runs, such as:
 * ```
 * command: git status
 * ── result ──
 * On branch main
 * ```
 */
object ToolCallBody {
    /** [call]'s body. */
    fun of(call: ToolCall): String {
        val input = call.input.joinToString("\n") { field -> if ('\n' in field.value) "${field.name}:\n${field.value}" else "${field.name}: ${field.value}" }
        val result = when {
            call.result == null -> "── running ──"
            call.result.isAnError -> "── failed ──\n${call.result.text.trimEnd()}"
            else -> "── result ──\n${call.result.text.trimEnd()}"
        }
        return "$input\n$result"
    }
}
