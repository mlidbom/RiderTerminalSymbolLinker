package dev.magnus.claudesessions.chat.transcript

import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.LinkAnnotation
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextLinkStyles
import androidx.compose.ui.text.buildAnnotatedString
import dev.magnus.claudesessions.chat.transcript.find.Highlight
import dev.magnus.claudesessions.chat.transcript.links.TranscriptLink

/**
 * What the [TranscriptPane] adds to a text as it shows it: Find's [highlights] in it, and the [links] the console filters
 * found in its item, each a link wherever its text shows in it, but where the text already links somewhere, as a link
 * of Claude's markdown does.
 *
 * Values: the transcript shows a text anew when what it adds to it changes.
 */
data class TextDecorations(val highlights: List<Highlight>, val links: List<TranscriptLink>) {
    /** [text], with the highlights and links, each link followed by [follow] when the user clicks it. */
    fun on(text: AnnotatedString, follow: (TranscriptLink) -> Unit): AnnotatedString {
        if (highlights.isEmpty() && links.isEmpty()) return text
        return buildAnnotatedString {
            append(text)
            val linked = text.getLinkAnnotations(0, text.length).map { it.start until it.end }.toMutableList()
            for (link in links) {
                for (range in placesOf(link.text, text.text).filter { range -> linked.none { it.overlaps(range) } }) {
                    addLink(LinkAnnotation.Clickable(LINK, TextLinkStyles(SpanStyle(color = TranscriptLook.link))) { follow(link) }, range.first, range.last + 1)
                    linked += range
                }
            }
            for (highlight in highlights.filter { it.range.last < text.length }) {
                addStyle(SpanStyle(background = if (highlight.isCurrent) TranscriptLook.foundCurrent else TranscriptLook.found), highlight.range.first, highlight.range.last + 1)
            }
        }
    }

    private fun IntRange.overlaps(other: IntRange): Boolean = first <= other.last && other.first <= last

    private fun placesOf(sought: String, text: String): List<IntRange> =
        generateSequence(text.indexOf(sought).takeIf { it >= 0 }) { previous -> text.indexOf(sought, previous + sought.length).takeIf { it >= 0 } }
            .map { it until it + sought.length }
            .toList()

    companion object {
        /** Nothing added. */
        val NONE = TextDecorations(emptyList(), emptyList())

        /** The tag of a link a console filter found. */
        private const val LINK = "a console filter's link"
    }
}
