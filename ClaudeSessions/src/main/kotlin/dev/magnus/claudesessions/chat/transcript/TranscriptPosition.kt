package dev.magnus.claudesessions.chat.transcript

/**
 * Where the user was in a [TranscriptPane]'s conversation, to show it there again after the IDE restarts: the row at
 * the top of the pane, [topRow], and how far it's scrolled past, [pixelsIntoIt]; and what the user opened, [opened]: the
 * tool calls and runs of them, by their rows' keys.
 *
 * @property topRow The key of the [TranscriptRow] at the top of the pane, or null when the pane follows the end of the
 *   conversation.
 */
data class TranscriptPosition(val topRow: String?, val pixelsIntoIt: Int, val opened: Set<String>) {
    companion object {
        /** At the end of the conversation, following it, nothing opened: where a new chat is. */
        val AT_THE_END = TranscriptPosition(topRow = null, pixelsIntoIt = 0, opened = emptySet())
    }
}
