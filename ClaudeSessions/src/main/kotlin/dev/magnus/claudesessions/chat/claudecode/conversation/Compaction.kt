package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * Where the conversation was compacted: Claude went on from a summary of what came before, which the chat still shows
 * above it, though Claude Code's own UI shows nothing of it any more.
 */
data class Compaction(override val id: String) : ConversationItem
