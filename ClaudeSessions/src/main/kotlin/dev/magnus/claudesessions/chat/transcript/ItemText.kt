package dev.magnus.claudesessions.chat.transcript

import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Compaction
import dev.magnus.claudesessions.chat.claudecode.conversation.ConversationItem
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall

/**
 * How the [TranscriptPane] shows a [ConversationItem]: its [text], in markdown, ending with a blank line, and the part
 * of it folded away, if any: a tool call's input and result, below its header.
 *
 * Each turn of Claude's starts with a `## Claude` heading, and each prompt with `## You`, so that the markdown
 * highlighting tells them apart, and a search for either steps from turn to turn.
 *
 * @property folded Where in [text] the fold starts and ends, or null when nothing of it is folded.
 */
class ItemText private constructor(val text: String, val folded: IntRange?) {
    companion object {
        /** How [item] is shown after [previous], the item shown before it, or first when that's null. */
        fun of(item: ConversationItem, previous: ConversationItem?): ItemText {
            val heading = if (item !is Prompt && item !is Compaction && previous.endsWhatClaudeSays) "## Claude\n" else ""
            return when (item) {
                is Prompt -> ItemText("## You\n${item.text.trimEnd()}\n\n", folded = null)
                is AssistantText -> ItemText("$heading${item.text.trimEnd()}\n\n", folded = null)
                is ToolCall -> ofTheToolCall(item, heading)
                is Compaction -> ItemText("───── The conversation was compacted here ─────\n\n", folded = null)
            }
        }

        /**
         * Whether what Claude says next starts a turn of its own: it does after a prompt, after a compaction, and
         * first.
         */
        private val ConversationItem?.endsWhatClaudeSays: Boolean get() = this == null || this is Prompt || this is Compaction

        /**
         * [call]'s header, its input and result folded away below it, in a fenced code block: a fence of more backticks
         * than any run of them inside, so that nothing in the input or the result ends it early.
         */
        private fun ofTheToolCall(call: ToolCall, heading: String): ItemText {
            val input = call.input.joinToString("\n") { field -> if ('\n' in field.value) "${field.name}:\n${field.value}" else "${field.name}: ${field.value}" }
            val result = when {
                call.result == null -> "── running ──"
                call.result.isAnError -> "── failed ──\n${call.result.text.trimEnd()}"
                else -> "── result ──\n${call.result.text.trimEnd()}"
            }
            val body = "$input\n$result"
            val fence = "`".repeat(maxOf(3, longestRunOfBackticksIn(body) + 1))
            val header = "$heading▸ ${ToolCallHeader.of(call)}"
            val folded = "\n$fence\n$body\n$fence"
            return ItemText("$header$folded\n\n", folded = header.length until header.length + folded.length)
        }

        private fun longestRunOfBackticksIn(text: String): Int = Regex("`+").findAll(text).maxOfOrNull { it.value.length } ?: 0
    }
}
