package dev.magnus.claudesessions.chat.claudecode.conversation

/** What Claude thought before it answered or acted: the [text] of its thinking, which Claude Code records only at times. */
data class Thinking(override val id: String, val text: String) : ConversationItem
