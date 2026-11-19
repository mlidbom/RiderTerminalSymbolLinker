package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * A thing that happened in a [Conversation]: a [Prompt], [AssistantText], a [ToolCall] or a [Compaction].
 *
 * Items are values: the chat compares the conversation it shows with a new one item by item, and shows anew only from
 * the first item that differs, as when a tool call's result has come.
 */
sealed interface ConversationItem {
    /** What tells the item from every other in its conversation, as long as it is in it: such as its entry's id in the transcript. */
    val id: String
}
