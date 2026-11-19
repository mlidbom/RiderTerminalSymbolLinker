package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * Where `/clear` ended a session, and the conversation went on in a new one, [goesOnIn], by its id: Claude knows nothing
 * of what came before it, which the chat still shows above it.
 */
data class Clearing(override val id: String, val goesOnIn: String) : ConversationItem
