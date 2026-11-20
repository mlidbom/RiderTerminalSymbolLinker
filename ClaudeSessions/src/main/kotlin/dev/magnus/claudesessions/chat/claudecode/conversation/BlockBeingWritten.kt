package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * The block of text or thinking the model writes in the turn under way, as far as it has streamed: what Claude Code's
 * own UI shows as it comes, before Claude Code writes the whole block to the transcript. The chat shows it at the
 * conversation's end, as its item, [asItem], until the conversation has the block.
 *
 * [id] is the block's, for as long as it is written: the turn's, the response's within the turn, and the block's within
 * the response.
 */
data class BlockBeingWritten(val id: String, val kind: Kind, val text: String) {
    enum class Kind { TEXT, THINKING }

    /** The item the chat shows of it: Claude's text, or Claude's thinking. */
    val asItem: ConversationItem
        get() = when (kind) {
            Kind.TEXT -> AssistantText(id, text)
            Kind.THINKING -> Thinking(id, text)
        }
}
