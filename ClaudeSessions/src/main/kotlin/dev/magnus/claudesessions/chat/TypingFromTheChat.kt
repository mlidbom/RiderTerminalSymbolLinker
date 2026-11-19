package dev.magnus.claudesessions.chat

import com.intellij.openapi.diagnostic.thisLogger
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChat
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.TypingOutcome
import dev.magnus.claudesessions.chat.messagebox.MessageBox
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.launch

/**
 * What the user gives the Claude Code behind a [Chat] from its [MessageBox]: messages, and the keys the box passes on.
 * Each is given to [claudeCode] in turn, in the order the user gave them, as Claude Code would get them from its own
 * input box. Giving one may take seconds, so the box is free for the next message at once.
 *
 * [afterEach] is told what came of each, with the messages Claude Code didn't get, to hand back to the user. When
 * Claude Code doesn't get a message, the messages waiting after it aren't given either, so that none overtakes it: they
 * are handed back with it, and the keys waiting among them are dropped, as they were meant for what the messages would
 * have started. A message left unsent in Claude Code's own input box isn't handed back: it is there.
 *
 * [whenWhatWaitsChanges] is told whenever [messagesNotYetGiven] or [messageBeingGiven] change, so that they're saved,
 * and come back as the IDE starts again.
 *
 * Runs in [scope], on the EDT.
 */
class TypingFromTheChat(
    private val claudeCode: ClaudeCodeBehindTheChat,
    scope: CoroutineScope,
    private val afterEach: (outcome: TypingOutcome, messagesClaudeCodeDidNotGet: List<Message>) -> Unit,
    private val whenWhatWaitsChanges: () -> Unit,
) {
    /** What the user gave, waiting to be given to Claude Code, in its order. */
    private val waiting = ArrayDeque<Typing>()

    /** Tells the giving that something waits. */
    private val somethingWaits = Channel<Unit>(Channel.CONFLATED)

    /** The message being given to Claude Code now, which it may or may not have got yet, or null while none is. */
    var messageBeingGiven: MessageMaybeSent? = null
        private set

    /** The messages the user sent that wait to be given to Claude Code, in their order. */
    val messagesNotYetGiven: List<Message> get() = waiting.filterIsInstance<Typing.Sending>().map { it.message }

    init {
        scope.launch {
            while (true) {
                somethingWaits.receive()
                while (true) give(waiting.removeFirstOrNull() ?: break)
            }
        }
    }

    /** Sends [message] to Claude Code once what the user gave it before is given. */
    fun send(message: Message) = wait(Typing.Sending(message))

    /** Interrupts Claude Code once what the user gave it before is given. */
    fun interrupt() = wait(Typing.Interrupting)

    /** Changes Claude Code's permission mode once what the user gave it before is given. */
    fun changeThePermissionMode() = wait(Typing.ChangingThePermissionMode)

    private fun wait(typing: Typing) {
        waiting.addLast(typing)
        somethingWaits.trySend(Unit)
        whenWhatWaitsChanges()
    }

    private suspend fun give(typing: Typing) {
        messageBeingGiven = (typing as? Typing.Sending)?.let { MessageMaybeSent(it.message, claudeCode.conversation.value?.items?.lastOrNull()?.id) }
        whenWhatWaitsChanges()
        val outcome = outcomeOfGiving(typing)
        messageBeingGiven = null
        afterEach(outcome, if (outcome == TypingOutcome.Typed || typing !is Typing.Sending) emptyList() else messagesHandedBackAfterRefusing(typing.message, outcome))
        whenWhatWaitsChanges()
    }

    /**
     * What came of giving Claude Code [typing]. A failure, such as a bug, refuses that one, so that what the user gives
     * Claude Code next is still given.
     */
    private suspend fun outcomeOfGiving(typing: Typing): TypingOutcome =
        try {
            typing.into(claudeCode)
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (failure: Exception) {
            thisLogger().error("Giving Claude Code what the chat typed failed", failure)
            TypingOutcome.NothingTyped(whyNotTyped = "Giving it to Claude Code failed: $failure", theTerminalShowsWhy = false)
        }

    private fun messagesHandedBackAfterRefusing(refused: Message, outcome: TypingOutcome): List<Message> =
        listOfNotNull(refused.takeIf { outcome is TypingOutcome.NothingTyped }) + takeTheMessagesWaiting()

    /** The messages waiting to be given, taken out of the waiting, which is then empty. */
    private fun takeTheMessagesWaiting(): List<Message> =
        messagesNotYetGiven.also { waiting.clear() }

    /** A message or a key the user gave Claude Code from the chat, waiting to be given to it. */
    private sealed interface Typing {
        suspend fun into(claudeCode: ClaudeCodeBehindTheChat): TypingOutcome

        data class Sending(val message: Message) : Typing {
            override suspend fun into(claudeCode: ClaudeCodeBehindTheChat) = claudeCode.send(message)
        }

        data object Interrupting : Typing {
            override suspend fun into(claudeCode: ClaudeCodeBehindTheChat) = claudeCode.interrupt()
        }

        data object ChangingThePermissionMode : Typing {
            override suspend fun into(claudeCode: ClaudeCodeBehindTheChat) = claudeCode.changeThePermissionMode()
        }
    }
}
