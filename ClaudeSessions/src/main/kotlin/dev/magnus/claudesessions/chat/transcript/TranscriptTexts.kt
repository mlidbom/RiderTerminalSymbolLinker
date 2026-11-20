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
import dev.magnus.claudesessions.chat.transcript.markdown.ClaudesMarkdown
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallBody
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallHeader

/**
 * The texts the [TranscriptPane] shows of each item of the conversation, which Find searches and the console filters
 * link, each an [ItemTextPart] of its item: a prompt's text; each part of what Claude said, once its markdown is
 * rendered, as [ClaudesMarkdown] reads it; a tool call's header and its body, open or not; and so on.
 *
 * It keeps each of Claude's texts' markdown, processed once, for as long as the item reads the same.
 *
 * Its methods run on the EDT.
 */
class TranscriptTexts {
    private val processor = ClaudesMarkdown.processor()

    private val markdowns = mutableMapOf<String, ClaudesMarkdown>()

    /** [item]'s markdown, processed. */
    fun markdownOf(item: AssistantText): ClaudesMarkdown =
        markdowns[item.id]?.takeIf { it.text == item.text } ?: ClaudesMarkdown(item.text, processor).also { markdowns[item.id] = it }

    /** [item]'s texts, in the order they show. */
    fun of(item: ConversationItem): List<String> = when (item) {
        is Prompt -> listOf(item.text.trimEnd())
        is PromptFromElsewhere -> listOf(item.text.trimEnd())
        is AssistantText -> markdownOf(item).texts
        is Thinking -> listOf(item.text.trimEnd())
        is ToolCall -> listOf(ToolCallHeader.of(item), ToolCallBody.of(item))
        is Interruption -> listOf(item.text.trimEnd())
        is Compaction -> listOf(COMPACTED)
        is Clearing -> listOf(CLEARED)
    }

    /** The texts of [items], in their order. */
    fun of(items: List<ConversationItem>): List<TranscriptText> =
        items.flatMap { item -> of(item).mapIndexed { index, text -> TranscriptText(ItemTextPart(item.id, index), text) } }

    /** Forgets the markdown of every item but [items], as the conversation went another way. */
    fun keepOnly(items: List<ConversationItem>) {
        markdowns.keys.retainAll(items.map { it.id }.toSet())
    }

    companion object {
        /** The index of a tool call's header among its texts. */
        const val HEADER = 0

        /** The index of a tool call's body among its texts. */
        const val BODY = 1

        /** What shows where the conversation was compacted. */
        const val COMPACTED = "The conversation was compacted here"

        /** What shows where `/clear` ended a session, and the conversation went on in a new one. */
        const val CLEARED = "/clear: the conversation went on in a new session"
    }
}
