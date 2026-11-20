package dev.magnus.claudesessions.chat.claudecode

import com.intellij.openapi.Disposable
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.claudecode.ClaudeCodesStatusLine
import dev.magnus.claudesessions.claudecode.TurnUnderWay
import kotlinx.coroutines.flow.StateFlow

/**
 * What the chat of a [ClaudeSessionTab] needs of the Claude Code that runs in the tab: its [conversation], its
 * [status], and a way to give it a message and keys as if they were typed into its own input box. The chat shows only
 * what this gives it, and sends only through it.
 *
 * The Claude tooling's hub provides it, as Magnus and the tooling owner agreed on 2026-11-19: it reads Claude Code's
 * transcripts and session records, and types into Claude Code. The sandbox IDE has a stand-in its specifications drive
 * as well: see [ClaudeCodeBehindTheChatProvider].
 *
 * Giving Claude Code a message or a key may take seconds, or minutes when other typing into the session has the turn:
 * each returns once Claude Code got it, or with why not, its [TypingOutcome].
 *
 * Disposing it ends whatever it follows: the chat disposes it as the chat goes, when its tab closes, or as the plugin
 * reloads.
 */
interface ClaudeCodeBehindTheChat : Disposable {
    /**
     * The conversation, as it is now, and as it changes: the whole of the tab's session, as the chat shows it. A
     * new value replaces the one before it. Null until it's known, as while the hub reads it: the chat then knows not
     * to take an empty conversation for one in which a message wasn't sent.
     */
    val conversation: StateFlow<Conversation?>

    /** Whether Claude Code runs in the tab, and if so, whether it is busy, waiting for the user, or idle. */
    val status: StateFlow<ClaudeCodeStatus>

    /** What the tab's Claude Code last showed under its input box: null while none runs, or it hasn't been told of yet. */
    val statusLine: StateFlow<ClaudeCodesStatusLine?>

    /** What the tab's Claude Code's turn under way does: null while no turn is, or none is told of. */
    val turnUnderWay: StateFlow<TurnUnderWay?>

    /**
     * Gives Claude Code [message], as if it were typed and pasted into its input box, and Enter pressed: Claude Code
     * runs it at once when idle, and queues it while busy. A message that starts with `/` runs the slash command.
     */
    suspend fun send(message: Message): TypingOutcome

    /** Interrupts what Claude Code is doing, as Escape in its input box does: only while it's busy, or waits in a dialog. */
    suspend fun interrupt(): TypingOutcome

    /** Changes Claude Code's permission mode to the next one, as Shift+Tab in its input box does. */
    suspend fun changeThePermissionMode(): TypingOutcome
}
