package dev.magnus.claudesessions.chat

import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.chat.claudecode.conversation.ConversationItem
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt

/**
 * A [message] the chat was giving Claude Code as the chat went, as when the IDE closed or crashed: Claude Code may have
 * got it, or not. The hub goes on typing a message once it has the console's turn, whatever becomes of the IDE; one
 * still waiting for the turn isn't typed. The conversation tells which: see [wasSentIn].
 *
 * @property lastItemBefore The id of the [ConversationItem] the conversation ended with as the chat began giving it, or
 *   null when it had none.
 */
data class MessageMaybeSent(val message: Message, val lastItemBefore: String?) {
    /**
     * Whether Claude Code got the message, as [conversation] shows: a [Prompt] of its text came after [lastItemBefore],
     * or anywhere, when that item is no longer the conversation's, as after a rewind. A message Claude Code queued while
     * Claude worked is in the conversation already, as the hub tells of it.
     */
    fun wasSentIn(conversation: Conversation): Boolean {
        val itemsSince = conversation.items.drop(conversation.items.indexOfFirst { it.id == lastItemBefore } + 1)
        return itemsSince.any { it is Prompt && it.text == message.text }
    }
}
