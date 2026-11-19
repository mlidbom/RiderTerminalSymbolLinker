package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * What Claude said: the [text] of its answer, or of what it says between tool calls about what it does, in markdown.
 */
data class AssistantText(override val id: String, val text: String) : ConversationItem
