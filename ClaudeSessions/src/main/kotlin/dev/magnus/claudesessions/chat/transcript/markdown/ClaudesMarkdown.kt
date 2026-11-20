package dev.magnus.claudesessions.chat.transcript.markdown

import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.transcript.TranscriptPane
import org.jetbrains.jewel.markdown.InlineMarkdown
import org.jetbrains.jewel.markdown.MarkdownBlock
import org.jetbrains.jewel.markdown.WithInlineMarkdown
import org.jetbrains.jewel.markdown.processing.MarkdownProcessor
import java.util.IdentityHashMap

/**
 * What Claude said, an [AssistantText], as the [TranscriptPane] renders its markdown: the [blocks] Jewel's markdown
 * renderer shows, and the texts of their parts, [texts], in the order they show, as they read once rendered, without
 * the markdown's marks: each paragraph's and heading's, in lists and quotes too, and each code block's. Not a table's:
 * Jewel keeps its table blocks' types to itself.
 *
 * Find searches the texts, and the pane decorates each as it renders, with Find's highlights and the console filters'
 * links: [partOf] tells which part a text the renderer renders is.
 */
class ClaudesMarkdown(val text: String, processor: MarkdownProcessor) {
    val blocks: List<MarkdownBlock> = processor.processMarkdownDocument(text)

    /** The inline content of each part that has some, by its identity, with the part's index. */
    private val partsByContent = IdentityHashMap<List<InlineMarkdown>, Int>()

    /** The texts of the parts, in their order. */
    val texts: List<String> = buildList { blocks.forEach { addTheTextsOf(it) } }

    /**
     * The index of the part whose inline content is [content], as the renderer gives it to render, or null when it's
     * none of them. By identity, since two parts can read the same, and by what it reads where the renderer gives a copy.
     */
    fun partOf(content: Iterable<InlineMarkdown>): Int? =
        partsByContent[content] ?: plainTextOf(content).let { text -> texts.indexOf(text).takeIf { it >= 0 } }

    private fun MutableList<String>.addTheTextsOf(block: MarkdownBlock) {
        when (block) {
            is MarkdownBlock.Paragraph -> addThePart(block.inlineContent)
            is MarkdownBlock.Heading -> addThePart(block.inlineContent)
            is MarkdownBlock.BlockQuote -> block.children.forEach { addTheTextsOf(it) }
            is MarkdownBlock.ListBlock -> block.children.forEach { addTheTextsOf(it) }
            is MarkdownBlock.ListItem -> block.children.forEach { addTheTextsOf(it) }
            is MarkdownBlock.CodeBlock -> add(block.content)
            // Such as a table, whose types are internal to Jewel: Find can't search them, nor decorate them.
            else -> Unit
        }
    }

    private fun MutableList<String>.addThePart(content: List<InlineMarkdown>) {
        partsByContent[content] = size
        add(plainTextOf(content))
    }

    companion object {
        /** What [content] reads once rendered, as Jewel's inline renderer renders it: a soft line break as a space. */
        fun plainTextOf(content: Iterable<InlineMarkdown>): String = buildString {
            fun append(inline: InlineMarkdown) {
                when (inline) {
                    is InlineMarkdown.Text -> append(inline.content)
                    is InlineMarkdown.Code -> append(inline.content)
                    is InlineMarkdown.HtmlInline -> append(inline.content)
                    is InlineMarkdown.HardLineBreak -> append('\n')
                    is InlineMarkdown.SoftLineBreak -> append(' ')
                    is InlineMarkdown.Image -> Unit
                    is WithInlineMarkdown -> inline.inlineContent.forEach(::append)
                }
            }
            content.forEach(::append)
        }

        /** The markdown processor the transcript renders Claude's texts with: CommonMark, with GitHub's tables, strikethrough and links of bare addresses. */
        fun processor(): MarkdownProcessor = MarkdownProcessor(
            listOf(
                org.jetbrains.jewel.markdown.extensions.github.tables.GitHubTableProcessorExtension,
                org.jetbrains.jewel.markdown.extensions.github.strikethrough.GitHubStrikethroughProcessorExtension(),
                org.jetbrains.jewel.markdown.extensions.autolink.AutolinkProcessorExtension,
            ),
        )

        /** [item]'s markdown, as [processor] processes it. */
        fun of(item: AssistantText, processor: MarkdownProcessor): ClaudesMarkdown = ClaudesMarkdown(item.text, processor)
    }
}
