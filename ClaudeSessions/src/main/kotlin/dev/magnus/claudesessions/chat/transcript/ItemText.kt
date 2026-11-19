package dev.magnus.claudesessions.chat.transcript

import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Clearing
import dev.magnus.claudesessions.chat.claudecode.conversation.Compaction
import dev.magnus.claudesessions.chat.claudecode.conversation.ConversationItem
import dev.magnus.claudesessions.chat.claudecode.conversation.Interruption
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import dev.magnus.claudesessions.chat.claudecode.conversation.Thinking
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall

/**
 * How the [TranscriptPane] shows a [ConversationItem]: its [text], in markdown, ending with a blank line, and the part
 * of it folded away, if any: a tool call's input and result, below its header, and Claude's thinking.
 *
 * Each turn of Claude's starts with a `## Claude` heading, each prompt of the user's with `## You`, and each prompt from
 * elsewhere with a heading that says where from, such as `## From deskmancer`, so that the markdown highlighting tells
 * them apart, and a search for any steps from turn to turn.
 *
 * @property folded Where in [text] the fold starts and ends, or null when nothing of it is folded.
 */
class ItemText private constructor(val text: String, val folded: IntRange?) {
    companion object {
        /** How [item] is shown after [previous], the item shown before it, or first when that's null. */
        fun of(item: ConversationItem, previous: ConversationItem?): ItemText {
            val heading = if (previous.endsWhatClaudeSays) "## Claude\n" else ""
            return when (item) {
                is Prompt -> ItemText("## You\n${item.text.trimEnd()}\n\n", folded = null)
                is PromptFromElsewhere -> ItemText("## ${headingOf(item.from)}\n${item.text.trimEnd()}\n\n", folded = null)
                is AssistantText -> ItemText("$heading${item.text.trimEnd()}\n\n", folded = null)
                is Thinking -> folded(header = "$heading▸ Thinking", body = item.text.trimEnd())
                is ToolCall -> ofTheToolCall(item, heading)
                is Interruption -> ItemText("*${item.text.trimEnd()}*\n\n", folded = null)
                is Compaction -> ItemText("───── The conversation was compacted here ─────\n\n", folded = null)
                is Clearing -> ItemText("───── /clear: the conversation went on in a new session ─────\n\n", folded = null)
            }
        }

        /** The heading of a prompt from [source]. */
        private fun headingOf(source: PromptFromElsewhere.Source): String = when (source) {
            is PromptFromElsewhere.Source.AnotherSession -> "From ${source.name ?: "another session"}"
            PromptFromElsewhere.Source.BackgroundTask -> "A background task finished"
            PromptFromElsewhere.Source.ScheduledWakeUp -> "A scheduled wake-up"
            PromptFromElsewhere.Source.ClaudeCode -> "From Claude Code"
            PromptFromElsewhere.Source.Program -> "From a program"
        }

        /** [header], with [body] folded away below it. */
        private fun folded(header: String, body: String): ItemText {
            val folded = "\n$body"
            return ItemText("$header$folded\n\n", folded = header.length until header.length + folded.length)
        }

        /**
         * Whether what Claude does next starts a turn of its own: it does after a prompt, after an interruption, after
         * the conversation was cut short for Claude, and first.
         */
        private val ConversationItem?.endsWhatClaudeSays: Boolean
            get() = this == null || this is Prompt || this is PromptFromElsewhere || this is Interruption || this is Compaction || this is Clearing

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
