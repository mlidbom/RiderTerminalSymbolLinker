package dev.magnus.claudesessions.chat.transcript.markdown

import androidx.compose.ui.text.AnnotatedString
import org.jetbrains.jewel.markdown.InlineMarkdown
import org.jetbrains.jewel.markdown.rendering.InlineMarkdownRenderer
import org.jetbrains.jewel.markdown.rendering.InlinesStyling
import dev.magnus.claudesessions.chat.transcript.TextDecorations
import dev.magnus.claudesessions.chat.transcript.TranscriptPane
import dev.magnus.claudesessions.chat.transcript.links.TranscriptLink

/**
 * Jewel's inline markdown renderer, [renderer], for one of Claude's texts, [markdown], with what the [TranscriptPane]
 * adds to each part of it it renders, [decorationsOf] the part, by its index among [markdown]'s texts.
 */
class DecoratingInlineRenderer(
    private val renderer: InlineMarkdownRenderer,
    private val markdown: ClaudesMarkdown,
    private val decorationsOf: (Int) -> TextDecorations,
    private val follow: (TranscriptLink) -> Unit,
) : InlineMarkdownRenderer {
    override fun renderAsAnnotatedString(inlineMarkdown: Iterable<InlineMarkdown>, styling: InlinesStyling, enabled: Boolean, onUrlClicked: ((String) -> Unit)?): AnnotatedString {
        val rendered = renderer.renderAsAnnotatedString(inlineMarkdown, styling, enabled, onUrlClicked)
        val part = markdown.partOf(inlineMarkdown) ?: return rendered
        return decorationsOf(part).on(rendered, follow)
    }
}
