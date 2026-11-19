package dev.magnus.claudesessions.observation

import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChat
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.flow.MutableStateFlow

/**
 * A [ClaudeCodeBehindTheChat] that the sandbox's specifications drive through the [ObservationChannel], in place of a
 * real one, which isn't built yet: the specifications set its [conversation] and its [status], and see what the chat
 * sent through it. So they specify the chat itself, whatever comes to provide it the conversation, and type for it.
 *
 * Until set, it says no Claude Code runs, so the tab shows its terminal, as a tab without a chat does.
 */
class StandInBehindTheChat : ClaudeCodeBehindTheChat {
    override val conversation = MutableStateFlow(Conversation.NONE)
    override val status = MutableStateFlow(ClaudeCodeStatus.NOT_RUNNING)

    /** The messages the chat sent, in their order. */
    val sent: MutableList<Message> = CopyOnWriteArrayList()

    /** How many times the chat interrupted Claude Code. */
    val interruptions = AtomicInteger()

    /** How many times the chat changed Claude Code's permission mode. */
    val permissionModeChanges = AtomicInteger()

    override fun send(message: Message) {
        sent += message
    }

    override fun interrupt() {
        interruptions.incrementAndGet()
    }

    override fun changeThePermissionMode() {
        permissionModeChanges.incrementAndGet()
    }

    override fun dispose() {}
}
