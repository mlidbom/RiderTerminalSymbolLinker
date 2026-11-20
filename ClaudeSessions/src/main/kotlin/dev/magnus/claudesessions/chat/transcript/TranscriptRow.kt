package dev.magnus.claudesessions.chat.transcript

import dev.magnus.claudesessions.chat.claudecode.conversation.ConversationItem
import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallRun

/**
 * A row of the [TranscriptPane], one under the other: what it shows of one item of the conversation, or the one line of a
 * [ToolCallRun]. Which rows there are depends on what the user opened: the calls of a run show as rows of their own
 * only while it's open.
 */
sealed interface TranscriptRow {
    /** What tells the row from every other in the transcript, as long as it shows the same thing. */
    val key: String

    /** The items of the conversation the row shows, in their order. */
    val items: List<ConversationItem>

    /**
     * An item shown as it is, with no line to open it by: what the user or Claude said, Claude's thinking, an
     * interruption, or where the conversation was cut short.
     */
    data class OfAnItem(val item: ConversationItem) : TranscriptRow {
        override val key: String get() = item.id
        override val items: List<ConversationItem> get() = listOf(item)
    }

    /**
     * A prompt the user didn't give, such as another session's message or a background task's notice: its one line,
     * [PromptFromElsewhereLine], with the prompt below it while it's open. Magnus, 2026-11-20, of background tasks: "I'd
     * like background tasks collapsed please", and of other sessions' messages: "Messaging between you I would also like
     * collapsed".
     */
    data class OfAPromptFromElsewhere(val prompt: PromptFromElsewhere, val isOpen: Boolean) : TranscriptRow {
        override val key: String get() = prompt.id
        override val items: List<ConversationItem> get() = listOf(prompt)
    }

    /**
     * A tool call: its header, with its body below it while it's open. [inARun] when it shows below the line of its
     * open [ToolCallRun], and by itself when Claude made no other call next to it.
     */
    data class OfAToolCall(val call: ToolCall, val inARun: Boolean, val isOpen: Boolean) : TranscriptRow {
        override val key: String get() = call.id
        override val items: List<ConversationItem> get() = listOf(call)
    }

    /** The one line of a [ToolCallRun], with its calls in rows of their own below it while it's open. */
    data class OfAToolCallRun(val run: ToolCallRun, val isOpen: Boolean) : TranscriptRow {
        override val key: String get() = run.key
        override val items: List<ConversationItem> get() = run.calls
    }

    companion object {
        /** The rows that show [items], with the rows whose keys are in [opened] open. */
        fun of(items: List<ConversationItem>, opened: Set<String>): List<TranscriptRow> = buildList {
            var index = 0
            while (index < items.size) {
                val item = items[index]
                if (item !is ToolCall) {
                    add(if (item is PromptFromElsewhere) OfAPromptFromElsewhere(item, isOpen = item.id in opened) else OfAnItem(item))
                    index++
                    continue
                }
                val calls = items.subList(index, items.size).takeWhile { it is ToolCall }.filterIsInstance<ToolCall>()
                index += calls.size
                if (calls.size == 1) add(OfAToolCall(item, inARun = false, isOpen = item.id in opened)) else addTheRun(ToolCallRun(calls), opened)
            }
        }

        /** Adds the line of [run], and its calls below it when its key is in [opened]. */
        private fun MutableList<TranscriptRow>.addTheRun(run: ToolCallRun, opened: Set<String>) {
            val isOpen = run.key in opened
            add(OfAToolCallRun(run, isOpen))
            if (isOpen) run.calls.forEach { add(OfAToolCall(it, inARun = true, isOpen = it.id in opened)) }
        }
    }
}
