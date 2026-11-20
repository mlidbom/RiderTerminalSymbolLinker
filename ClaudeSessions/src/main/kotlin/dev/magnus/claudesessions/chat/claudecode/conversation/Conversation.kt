package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * The conversation of a tab's session as the chat shows it: what the user said, what Claude said and did, and where
 * the conversation was compacted, in the order they happened. Only the conversation: what Claude Code records for
 * itself, such as hooks' reminders and its requests to the model, isn't in it.
 */
data class Conversation(val items: List<ConversationItem>) {
    /** The tool calls made since the last prompt, the user's or from elsewhere: the commands the turn has run, or runs. */
    val toolCallsOfTheLastTurn: Int get() = items.takeLastWhile { it !is Prompt && it !is PromptFromElsewhere }.count { it is ToolCall }

    /** The conversation with [block], the one the model writes now, at its end, as the chat shows it: itself, while none is. */
    fun withAtItsEnd(block: BlockBeingWritten?): Conversation = if (block == null) this else Conversation(items + block.asItem)
}
