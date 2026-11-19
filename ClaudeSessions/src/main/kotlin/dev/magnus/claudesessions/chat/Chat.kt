package dev.magnus.claudesessions.chat

import com.intellij.openapi.Disposable
import com.intellij.openapi.application.EDT
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.Disposer
import com.intellij.ui.OnePixelSplitter
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChat
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.TypingOutcome
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.chat.messagebox.MessageBox
import dev.magnus.claudesessions.chat.saving.SavedChat
import dev.magnus.claudesessions.chat.transcript.TranscriptPane
import java.awt.BorderLayout
import javax.swing.JComponent
import javax.swing.JPanel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.launch

/**
 * The chat of a [ClaudeSessionTab]: its session's conversation, in the [TranscriptPane], and the [MessageBox] the user
 * writes to Claude in, with the [StatusLine] between them. Everyday talk with Claude happens here, in the IDE's own
 * editors, and Claude Code's own UI, in the tab's terminal, is a key away, for what the chat doesn't do. It shows what
 * [claudeCode] gives it, and sends through it, by way of [TypingFromTheChat].
 *
 * What Claude Code didn't get comes back: a message goes back into the box, the status line says why, and when what is
 * in the way shows in Claude Code's own UI, [showTheTerminal] shows it.
 *
 * [whenItChanges] is told whenever what the chat holds that is saved changes: the box, the messages waiting to be
 * given, and where the transcript is. A chat [bringBack] brings back as it was, as after the IDE restarted.
 *
 * Magnus asked for it on 2026-11-19, as text editing and search in Claude Code's own UI fall short: "Virtually NONE of
 * the absolutely standard text editing stuff works", and "Normal search in the transcript sucks".
 *
 * Disposing it disposes [claudeCode] too.
 */
class Chat(project: Project, val claudeCode: ClaudeCodeBehindTheChat, private val showTheTerminal: () -> Unit, private val whenItChanges: () -> Unit) : Disposable {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.EDT)

    val transcript = TranscriptPane(project, this)
    val statusLine = StatusLine()
    private val typing = TypingFromTheChat(claudeCode, scope, ::handBack, whenWhatWaitsChanges = whenItChanges)
    val messageBox = MessageBox(project, typing, afterSending = { if (it.isASlashCommand) showTheTerminal() }, this)

    /**
     * The messages that were being given to Claude Code as the chat went before the IDE restarted, until the
     * conversation tells whether Claude Code got them: each it didn't get then comes back into the box.
     */
    private var maybeSentAsTheChatWent = emptyList<MessageMaybeSent>()

    val component: JComponent = OnePixelSplitter(true, 0.8f).apply {
        firstComponent = transcript.component
        secondComponent = JPanel(BorderLayout()).apply {
            add(statusLine.component, BorderLayout.NORTH)
            add(messageBox.component, BorderLayout.CENTER)
        }
    }

    /** The messages the user sent that Claude Code wasn't given yet, in their order. */
    val messagesNotSent: List<Message> get() = typing.messagesNotYetGiven

    /** The messages Claude Code may have got, or not, as the conversation will tell. */
    val messagesMaybeSent: List<MessageMaybeSent> get() = maybeSentAsTheChatWent + listOfNotNull(typing.messageBeingGiven)

    init {
        Disposer.register(this, claudeCode)
        messageBox.whenItChanges(whenItChanges)
        transcript.whenItChanges(whenItChanges)
        scope.launch {
            claudeCode.conversation.filterNotNull().collect { conversation ->
                transcript.show(conversation)
                statusLine.show(conversation)
                putBackWhatClaudeCodeDidNotGet(conversation)
            }
        }
        scope.launch { claudeCode.status.collect(statusLine::show) }
    }

    /**
     * Brings the chat back as [asItWas], as after the IDE restarted: what the box held, the messages that weren't given
     * to Claude Code, back in it, as messages Claude Code didn't get come back, and where the transcript was. Each
     * message that was being given comes back too, once the conversation shows that Claude Code didn't get it.
     */
    fun bringBack(asItWas: SavedChat) {
        messageBox.restore(asItWas.draft)
        transcript.restore(asItWas.transcript)
        putBackAsNotSent(asItWas.notSent)
        maybeSentAsTheChatWent = asItWas.maybeSent
        claudeCode.conversation.value?.let(::putBackWhatClaudeCodeDidNotGet)
    }

    /** Hands back to the user what Claude Code didn't get of what the chat gave it, as [TypingFromTheChat] tells. */
    private fun handBack(outcome: TypingOutcome, messagesClaudeCodeDidNotGet: List<Message>) {
        messageBox.putBack(messagesClaudeCodeDidNotGet)
        statusLine.showWhyNotTyped(outcome.whyNotTyped)
        if (outcome.theTerminalShowsWhy) showTheTerminal()
    }

    /** Puts the messages of [maybeSentAsTheChatWent] that [conversation] shows Claude Code didn't get back into the box. */
    private fun putBackWhatClaudeCodeDidNotGet(conversation: Conversation) {
        if (maybeSentAsTheChatWent.isEmpty()) return
        val notGot = maybeSentAsTheChatWent.filterNot { it.wasSentIn(conversation) }.map { it.message }
        maybeSentAsTheChatWent = emptyList()
        putBackAsNotSent(notGot)
        whenItChanges()
    }

    /** Puts [messages] back into the box, ahead of what it holds, as the IDE closed before Claude Code got them. */
    private fun putBackAsNotSent(messages: List<Message>) {
        if (messages.isEmpty()) return
        messageBox.putBack(messages)
        statusLine.showWhyNotTyped(if (messages.size == 1) "Not sent: the IDE closed before Claude Code got this message." else "Not sent: the IDE closed before Claude Code got these messages.")
    }

    override fun dispose() {
        scope.cancel()
    }
}
