package dev.magnus.claudesessions.chat.transcript

/**
 * Where the user was in a [TranscriptPane]'s conversation, to show it there again after the IDE restarts: the item at
 * the top of the pane, [topItem], and how many of its lines are scrolled past, [linesIntoIt]; and the items whose folds
 * the user opened, [openFolds], by their ids.
 *
 * @property topItem The id of the item at the top of the pane, or null when the pane follows the end of the conversation.
 */
data class TranscriptPosition(val topItem: String?, val linesIntoIt: Int, val openFolds: Set<String>) {
    companion object {
        /** At the end of the conversation, following it, every fold closed: where a new chat is. */
        val AT_THE_END = TranscriptPosition(topItem = null, linesIntoIt = 0, openFolds = emptySet())
    }
}
