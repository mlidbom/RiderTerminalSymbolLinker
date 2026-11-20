package dev.magnus.claudesessions.chat.transcript.find

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import dev.magnus.claudesessions.chat.transcript.ItemTextPart
import dev.magnus.claudesessions.chat.transcript.TranscriptPane
import dev.magnus.claudesessions.chat.transcript.TranscriptText
import dev.magnus.claudesessions.chat.transcript.TranscriptTexts

/**
 * Find in the [TranscriptPane], as Find in an editor: the text it seeks, [query], every place the transcript's texts hold
 * it, case aside, [found], and the one the user is at, [current], which [show] shows, opening what hides it, such as a
 * tool call's body. It searches every text the transcript shows, as [TranscriptTexts] lists them, collapsed or not.
 *
 * Its state is Compose's, so that the transcript's highlights follow it.
 *
 * Magnus asked for it with the transcript's rendered markdown, on 2026-11-20, as an editor's Find was what the chat lost
 * by no longer showing the transcript in an editor.
 *
 * Its methods run on the EDT.
 */
class TranscriptFind(private val texts: () -> List<TranscriptText>, private val show: (FoundText) -> Unit) {
    var query: String by mutableStateOf("")
        private set

    var found: List<FoundText> by mutableStateOf(emptyList())
        private set

    /** The index in [found] of the place the user is at, or null when Find found nothing, or seeks nothing. */
    var current: Int? by mutableStateOf(null)
        private set

    /** Each text's places of [found], by the text, each with its index in [found]. */
    private var foundIn: Map<ItemTextPart, List<IndexedValue<FoundText>>> by mutableStateOf(emptyMap())

    /** Seeks [query], from the item [fromItem], by its id, on: the first place there or after it, or else the first of all. */
    fun seek(query: String, fromItem: String?) {
        this.query = query
        searchAgain()
        val itemsInOrder = texts().map { it.part.item }.distinct()
        val from = fromItem?.let(itemsInOrder::indexOf)?.takeIf { it >= 0 } ?: 0
        current = found.indexOfFirst { itemsInOrder.indexOf(it.part.item) >= from }.takeIf { it >= 0 } ?: found.indices.firstOrNull()
        showTheCurrent()
    }

    /** Goes to the next place, or the first after the last. */
    fun next() = goBy(1)

    /** Goes to the place before, or the last before the first. */
    fun previous() = goBy(-1)

    /**
     * Searches again, as the transcript's texts changed: the user stays at the place they were at, if it still holds
     * what Find seeks, and else at the one now in its place.
     */
    fun searchAgain() {
        val was = current?.let(found::getOrNull)
        found = if (query.isEmpty()) emptyList() else texts().flatMap(::placesIn)
        foundIn = found.withIndex().groupBy { it.value.part }
        current = if (found.isEmpty()) null else was?.let(found::indexOf)?.takeIf { it >= 0 } ?: current?.coerceAtMost(found.lastIndex)
    }

    /** Stops seeking: nothing is highlighted any more. */
    fun stop() {
        query = ""
        searchAgain()
    }

    /** Where [part] holds what Find seeks. */
    fun highlightsIn(part: ItemTextPart): List<Highlight> = foundIn[part].orEmpty().map { Highlight(it.value.range, isCurrent = it.index == current) }

    private fun goBy(step: Int) {
        val now = current ?: return
        current = (now + step).mod(found.size)
        showTheCurrent()
    }

    private fun showTheCurrent() {
        current?.let(found::getOrNull)?.let(show)
    }

    private fun placesIn(text: TranscriptText): List<FoundText> =
        generateSequence(text.text.indexOf(query, ignoreCase = true).takeIf { it >= 0 }) { previous -> text.text.indexOf(query, previous + query.length, ignoreCase = true).takeIf { it >= 0 } }
            .map { FoundText(text.part, it until it + query.length) }
            .toList()
}
