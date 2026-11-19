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
import dev.magnus.claudesessions.chat.messagebox.MessageBox
import dev.magnus.claudesessions.chat.transcript.TranscriptPane
import java.awt.BorderLayout
import javax.swing.JComponent
import javax.swing.JPanel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
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
 * Magnus asked for it on 2026-11-19, as text editing and search in Claude Code's own UI fall short: "Virtually NONE of
 * the absolutely standard text editing stuff works", and "Normal search in the transcript sucks".
 *
 * Disposing it disposes [claudeCode] too.
 */
class Chat(project: Project, val claudeCode: ClaudeCodeBehindTheChat, private val showTheTerminal: () -> Unit) : Disposable {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.EDT)

    val transcript = TranscriptPane(project, this)
    val statusLine = StatusLine()
    private val typing = TypingFromTheChat(claudeCode, scope, ::handBack)
    val messageBox = MessageBox(project, typing, afterSending = { if (it.isASlashCommand) showTheTerminal() }, this)

    val component: JComponent = OnePixelSplitter(true, 0.8f).apply {
        firstComponent = transcript.component
        secondComponent = JPanel(BorderLayout()).apply {
            add(statusLine.component, BorderLayout.NORTH)
            add(messageBox.component, BorderLayout.CENTER)
        }
    }

    init {
        Disposer.register(this, claudeCode)
        scope.launch {
            claudeCode.conversation.collect { conversation ->
                transcript.show(conversation)
                statusLine.show(conversation)
            }
        }
        scope.launch { claudeCode.status.collect(statusLine::show) }
    }

    /** Hands back to the user what Claude Code didn't get of what the chat gave it, as [TypingFromTheChat] tells. */
    private fun handBack(outcome: TypingOutcome, messagesClaudeCodeDidNotGet: List<Message>) {
        messageBox.putBack(messagesClaudeCodeDidNotGet)
        statusLine.showWhyNotTyped(outcome.whyNotTyped)
        if (outcome.theTerminalShowsWhy) showTheTerminal()
    }

    override fun dispose() {
        scope.cancel()
    }
}
