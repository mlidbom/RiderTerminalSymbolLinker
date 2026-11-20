package dev.magnus.claudesessions.chat.transcript.markdown

import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.isSpecified
import org.jetbrains.jewel.intui.markdown.bridge.styling.create
import org.jetbrains.jewel.markdown.rendering.InlinesStyling
import org.jetbrains.jewel.markdown.rendering.MarkdownStyling

/**
 * How the transcript styles Claude's markdown: as the IDE does, through Jewel's bridge, but for its headings, which are
 * a little larger than the text, and bold, with little space around them, as the desktop app shows them. The bridge's
 * are GitHub's, made for documents: up to twice the text's size, underlined, with a wide space below them. Magnus,
 * 2026-11-20: "It is truly huge, the spacing below it too".
 */
object TranscriptMarkdownStyling {
    /** Above a heading, besides the space between any two blocks; below it, only that space. */
    private val AROUND_A_HEADING = PaddingValues(top = 6.dp)

    /** The bridge's styling of text in [textStyle], and of code in [editorTextStyle], with the transcript's headings. */
    fun of(textStyle: TextStyle, editorTextStyle: TextStyle): MarkdownStyling =
        MarkdownStyling.create(baseTextStyle = textStyle, editorTextStyle = editorTextStyle, heading = headingsOf(textStyle, editorTextStyle))

    private fun headingsOf(textStyle: TextStyle, editorTextStyle: TextStyle): MarkdownStyling.Heading {
        fun inlines(times: Float) = InlinesStyling.create(textStyle.scaled(times).copy(fontWeight = FontWeight.SemiBold), editorTextStyle.scaled(times))
        return MarkdownStyling.Heading(
            MarkdownStyling.Heading.H1(inlines(1.3f), 0.dp, Color.Unspecified, 0.dp, AROUND_A_HEADING),
            MarkdownStyling.Heading.H2(inlines(1.2f), 0.dp, Color.Unspecified, 0.dp, AROUND_A_HEADING),
            MarkdownStyling.Heading.H3(inlines(1.1f), 0.dp, Color.Unspecified, 0.dp, AROUND_A_HEADING),
            MarkdownStyling.Heading.H4(inlines(1f), 0.dp, Color.Unspecified, 0.dp, AROUND_A_HEADING),
            MarkdownStyling.Heading.H5(inlines(1f), 0.dp, Color.Unspecified, 0.dp, AROUND_A_HEADING),
            MarkdownStyling.Heading.H6(inlines(1f), 0.dp, Color.Unspecified, 0.dp, AROUND_A_HEADING),
        )
    }

    /** This style, its font [times] the size; its line height too, unless the font sets it. */
    private fun TextStyle.scaled(times: Float): TextStyle =
        copy(fontSize = fontSize * times, lineHeight = if (lineHeight.isSpecified) lineHeight * times else lineHeight)
}
