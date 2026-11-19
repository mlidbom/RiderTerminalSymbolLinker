package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * Where the user interrupted Claude, as with Escape: [text] is Claude Code's note of it, such as
 * `[Request interrupted by user]`.
 */
data class Interruption(override val id: String, val text: String) : ConversationItem
