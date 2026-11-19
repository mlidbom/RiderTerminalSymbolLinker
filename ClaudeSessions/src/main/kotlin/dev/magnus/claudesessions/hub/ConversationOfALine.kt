package dev.magnus.claudesessions.hub

import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.claudecode.ClaudeSessionId

/**
 * The conversation of a session's line, as the hub tells of it: the sessions a `/clear` ended before it, then its own,
 * then the one a clear of it began, each one's conversation after the other's. Any session of the line names the same
 * line, and has the same conversation.
 *
 * @property sessions The sessions the conversation is of, those of the line the hub has told of so far.
 */
data class ConversationOfALine(val conversation: Conversation, val sessions: Set<ClaudeSessionId>)
