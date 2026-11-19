package dev.magnus.claudesessions.chat.saving

import dev.magnus.claudesessions.chat.MessageMaybeSent
import dev.magnus.claudesessions.chat.TabCard
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.messagebox.Draft
import dev.magnus.claudesessions.chat.transcript.TranscriptPosition

/**
 * A tab's chat as the [ChatKeeper] last saved it, so that it comes back as it was after the IDE restarts or crashes, or
 * reloads the plugin: nothing the user wrote is lost, nor where they were.
 *
 * @property draft What the input box held, unsent.
 * @property notSent The messages the user sent that weren't given to Claude Code yet, as they waited their turn: they
 *   come back into the box, as messages Claude Code didn't get do.
 * @property maybeSent The messages that were being given to Claude Code, which it may have got: each comes back into the
 *   box only once the conversation shows it didn't.
 * @property transcript Where the transcript was scrolled to, and which tool calls were open.
 * @property cardTheUserChose The card the user chose to show, the chat or the terminal, or null when the tab chose it
 *   by itself.
 */
data class SavedChat(
    val draft: Draft,
    val notSent: List<Message>,
    val maybeSent: List<MessageMaybeSent>,
    val transcript: TranscriptPosition,
    val cardTheUserChose: TabCard?,
)
