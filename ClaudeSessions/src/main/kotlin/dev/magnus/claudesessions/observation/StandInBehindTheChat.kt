package dev.magnus.claudesessions.observation

import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChat
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.TypingOutcome
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first

/**
 * A [ClaudeCodeBehindTheChat] that the sandbox's specifications drive through the [ObservationChannel], in place of a
 * real one: the specifications set its [conversation] and its [status], see what the chat sent through it, and say
 * what comes of what the chat gives it. So they specify the chat itself, whatever comes to provide it the conversation,
 * and type for it.
 *
 * Until set, it says no Claude Code runs, so the tab shows its terminal, as a tab without a chat does.
 */
class StandInBehindTheChat : ClaudeCodeBehindTheChat {
    override val conversation = MutableStateFlow(Conversation.NONE)
    override val status = MutableStateFlow(ClaudeCodeStatus.NOT_RUNNING)

    /** The messages Claude Code got from the chat, in their order. */
    val sent: MutableList<Message> = CopyOnWriteArrayList()

    /** How many times Claude Code was interrupted from the chat. */
    val interruptions = AtomicInteger()

    /** How many times Claude Code's permission mode was changed from the chat. */
    val permissionModeChanges = AtomicInteger()

    /** What comes of what the chat gives the stand-in: Claude Code gets it, until the specifications say otherwise. */
    @Volatile
    var outcome: TypingOutcome = TypingOutcome.Typed

    private val holding = MutableStateFlow(false)

    /**
     * Has the stand-in hold what the chat gives it, until let go, so that the specifications see what the chat does
     * meanwhile: as when typing into the real Claude Code waits for its turn.
     */
    fun hold(hold: Boolean) {
        holding.value = hold
    }

    override suspend fun send(message: Message): TypingOutcome = whenLetGo { sent += message }

    override suspend fun interrupt(): TypingOutcome = whenLetGo { interruptions.incrementAndGet() }

    override suspend fun changeThePermissionMode(): TypingOutcome = whenLetGo { permissionModeChanges.incrementAndGet() }

    /** Once nothing is held: [outcome], and [whenTyped] done first when it is [TypingOutcome.Typed]. */
    private suspend fun whenLetGo(whenTyped: () -> Unit): TypingOutcome {
        holding.first { !it }
        return outcome.also { if (it == TypingOutcome.Typed) whenTyped() }
    }

    override fun dispose() {}
}
