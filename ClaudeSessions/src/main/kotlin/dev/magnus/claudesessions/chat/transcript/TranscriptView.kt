package dev.magnus.claudesessions.chat.transcript

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.relocation.BringIntoViewRequester
import androidx.compose.foundation.relocation.bringIntoViewRequester
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.selection.DisableSelection
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.PointerIcon
import androidx.compose.ui.input.pointer.pointerHoverIcon
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Clearing
import dev.magnus.claudesessions.chat.claudecode.conversation.Compaction
import dev.magnus.claudesessions.chat.claudecode.conversation.Interruption
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import dev.magnus.claudesessions.chat.claudecode.conversation.Thinking
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.transcript.links.TranscriptLink
import dev.magnus.claudesessions.chat.transcript.markdown.ClaudesMarkdown
import dev.magnus.claudesessions.chat.transcript.markdown.DecoratingInlineRenderer
import dev.magnus.claudesessions.chat.transcript.markdown.TranscriptMarkdownStyling
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallBody
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallHeader
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallStatus
import org.jetbrains.jewel.foundation.theme.JewelTheme
import org.jetbrains.jewel.intui.markdown.bridge.ProvideMarkdownStyling
import org.jetbrains.jewel.intui.markdown.bridge.styling.extensions.github.tables.create
import org.jetbrains.jewel.markdown.Markdown
import org.jetbrains.jewel.markdown.extensions.github.strikethrough.GitHubStrikethroughRendererExtension
import org.jetbrains.jewel.markdown.extensions.github.tables.GfmTableStyling
import org.jetbrains.jewel.markdown.extensions.github.tables.GitHubTableRendererExtension
import org.jetbrains.jewel.markdown.extensions.markdownStyling
import org.jetbrains.jewel.markdown.rendering.DefaultInlineMarkdownRenderer
import org.jetbrains.jewel.markdown.rendering.DefaultMarkdownBlockRenderer
import org.jetbrains.jewel.markdown.rendering.MarkdownBlockRenderer
import org.jetbrains.jewel.ui.component.Icon
import org.jetbrains.jewel.ui.component.Text
import org.jetbrains.jewel.ui.icons.AllIconsKeys
import org.jetbrains.jewel.ui.component.VerticallyScrollableContainer

/**
 * The [TranscriptPane]'s conversation, in Compose: its rows, one under the other, in a list that composes only the
 * rows on the screen, its text selectable across them. What the user said shows shaded, at the right, what Claude said
 * as rendered markdown, each tool call, or run of them, as one line that opens to what it ran and got, and each prompt
 * from elsewhere as one line that opens to the prompt.
 *
 * @param listState Where the list is scrolled to: the pane's, but for a picture of it, which scrolls a copy.
 */
@Composable
internal fun TranscriptView(pane: TranscriptPane, listState: LazyListState = pane.listState) {
    ProvideMarkdownStyling(pane.project) {
        VerticallyScrollableContainer(listState, Modifier.fillMaxSize().background(TranscriptLook.background)) {
            SelectionContainer {
                LazyColumn(Modifier.fillMaxSize(), state = listState, contentPadding = PaddingValues(start = 16.dp, end = 24.dp, top = 8.dp, bottom = 16.dp)) {
                    items(pane.rows, key = TranscriptRow::key) { row -> TranscriptRowView(pane, row) }
                }
            }
        }
    }
}

@Composable
private fun TranscriptRowView(pane: TranscriptPane, row: TranscriptRow) {
    when (row) {
        is TranscriptRow.OfAnItem -> when (val item = row.item) {
            is Prompt -> UserSaid(pane, item)
            is AssistantText -> ClaudeSaid(pane, item)
            is Thinking -> Thought(pane, item)
            is Interruption -> DecoratedText(pane, ItemTextPart(item.id, 0), item.text.trimEnd(), JewelTheme.defaultTextStyle.copy(color = TranscriptLook.secondaryText, fontStyle = FontStyle.Italic), Modifier.padding(vertical = 4.dp))
            is Compaction -> Divider(pane, ItemTextPart(item.id, 0), TranscriptTexts.COMPACTED)
            is Clearing -> Divider(pane, ItemTextPart(item.id, 0), TranscriptTexts.CLEARED)
            is PromptFromElsewhere -> error("A prompt from elsewhere shows in a row of its own")
            is ToolCall -> error("A tool call shows in a row of its own")
        }
        is TranscriptRow.OfAPromptFromElsewhere -> PromptFromElsewhereView(pane, row)
        is TranscriptRow.OfAToolCall -> ToolCallView(pane, row)
        is TranscriptRow.OfAToolCallRun -> ToolCallRunView(pane, row)
    }
}

/**
 * What the user said: shaded, at the right, as wide as its text up to most of the transcript's width, as the desktop
 * app shows it. Magnus, 2026-11-20: "If things that I type could be right aligned that would also really help with
 * visibility".
 */
@Composable
private fun UserSaid(pane: TranscriptPane, prompt: Prompt) {
    BoxWithConstraints(Modifier.fillMaxWidth().padding(top = 12.dp, bottom = 6.dp), contentAlignment = Alignment.CenterEnd) {
        Box(Modifier.widthIn(max = maxWidth * 0.85f).background(TranscriptLook.shaded, RoundedCornerShape(8.dp)).padding(horizontal = 12.dp, vertical = 8.dp)) {
            DecoratedText(pane, ItemTextPart(prompt.id, 0), prompt.text.trimEnd(), JewelTheme.defaultTextStyle)
        }
    }
}

/** A prompt the user didn't give: its one line, and the prompt below it while it's open. */
@Composable
private fun PromptFromElsewhereView(pane: TranscriptPane, row: TranscriptRow.OfAPromptFromElsewhere) {
    val prompt = row.prompt
    Column(Modifier.fillMaxWidth().padding(vertical = 2.dp)) {
        Header(isOpen = row.isOpen, onClick = { pane.toggle(row.key) }) {
            Text(PromptFromElsewhereLine.of(prompt), Modifier.weight(1f, fill = false), color = TranscriptLook.secondaryText, style = JewelTheme.defaultTextStyle, maxLines = 1, overflow = TextOverflow.Ellipsis)
        }
        if (row.isOpen) OpenBody { DecoratedText(pane, ItemTextPart(prompt.id, 0), prompt.text.trimEnd(), JewelTheme.defaultTextStyle) }
    }
}

/** What Claude said, its markdown rendered. */
@Composable
private fun ClaudeSaid(pane: TranscriptPane, item: AssistantText) {
    val markdown = pane.texts.markdownOf(item)
    val decorations = markdown.texts.indices.map { pane.decorationsOf(ItemTextPart(item.id, it)) }
    val textStyle = JewelTheme.defaultTextStyle
    val editorTextStyle = JewelTheme.editorTextStyle
    val styling = remember(textStyle, editorTextStyle) { TranscriptMarkdownStyling.of(textStyle, editorTextStyle) }
    // Anew whenever what the pane adds to a part changes: Jewel's renderer remembers each part as it rendered it.
    key(markdown, decorations) {
        val renderer = remember(styling) { blockRendererFor(markdown, styling, decorations, pane::follow) }
        Markdown(markdown.blocks, markdown.text, Modifier.fillMaxWidth().padding(vertical = 4.dp), enabled = true, selectable = false, onUrlClick = pane::browse, blockRenderer = renderer)
    }
}

/** Jewel's block renderer, as the IDE styles it, with GitHub's tables and strikethrough, which decorates each part of [markdown] with [decorations]. */
private fun blockRendererFor(
    markdown: ClaudesMarkdown,
    styling: org.jetbrains.jewel.markdown.rendering.MarkdownStyling,
    decorations: List<TextDecorations>,
    follow: (TranscriptLink) -> Unit,
): MarkdownBlockRenderer {
    val extensions = listOf(GitHubTableRendererExtension(GfmTableStyling.create(), styling), GitHubStrikethroughRendererExtension)
    val inlineRenderer = DecoratingInlineRenderer(DefaultInlineMarkdownRenderer(extensions), markdown, { decorations.getOrElse(it) { TextDecorations.NONE } }, follow)
    return DefaultMarkdownBlockRenderer(styling, extensions, inlineRenderer)
}

/** What Claude thought, under a caption, dimmed. */
@Composable
private fun Thought(pane: TranscriptPane, item: Thinking) {
    Column(Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
        DisableSelection { Text("Thinking", color = TranscriptLook.secondaryText, style = JewelTheme.defaultTextStyle) }
        DecoratedText(pane, ItemTextPart(item.id, 0), item.text.trimEnd(), JewelTheme.defaultTextStyle.copy(color = TranscriptLook.secondaryText, fontStyle = FontStyle.Italic))
    }
}

/** A tool call's header, and its body below it while it's open; indented below the line of its run when it's in one. */
@Composable
private fun ToolCallView(pane: TranscriptPane, row: TranscriptRow.OfAToolCall) {
    val call = row.call
    Column(Modifier.fillMaxWidth().padding(start = if (row.inARun) 20.dp else 0.dp, top = 2.dp, bottom = 2.dp)) {
        Header(isOpen = row.isOpen, onClick = { pane.toggle(row.key) }) {
            DecoratedText(pane, ItemTextPart(call.id, TranscriptTexts.HEADER), ToolCallHeader.of(call), JewelTheme.defaultTextStyle.copy(color = TranscriptLook.secondaryText))
            Status(ToolCallStatus.of(listOf(call)))
        }
        if (row.isOpen) OpenBody { DecoratedText(pane, ItemTextPart(call.id, TranscriptTexts.BODY), ToolCallBody.of(call), JewelTheme.editorTextStyle) }
    }
}

/** What a line that is open shows below it, indented under its chevron, shaded. */
@Composable
private fun OpenBody(content: @Composable () -> Unit) {
    Box(Modifier.fillMaxWidth().padding(start = 16.dp, top = 4.dp, bottom = 4.dp).background(TranscriptLook.shaded, RoundedCornerShape(4.dp)).padding(8.dp)) {
        content()
    }
}

/** The one line of a run of tool calls. */
@Composable
private fun ToolCallRunView(pane: TranscriptPane, row: TranscriptRow.OfAToolCallRun) {
    Box(Modifier.fillMaxWidth().padding(vertical = 2.dp)) {
        Header(isOpen = row.isOpen, onClick = { pane.toggle(row.key) }) {
            val status = ToolCallStatus.of(row.run.calls)
            Text(row.run.summary.removeSuffix(status.suffix), color = TranscriptLook.secondaryText, style = JewelTheme.defaultTextStyle)
            Status(status)
        }
    }
}

/** How tool calls went, after their line: in the error's color when any failed. */
@Composable
private fun Status(status: ToolCallStatus) {
    if (status.suffix.isNotEmpty()) Text(status.suffix, color = if (status.failed > 0) TranscriptLook.error else TranscriptLook.secondaryText, style = JewelTheme.defaultTextStyle)
}

/** A line that opens and closes what is below it when clicked, with a chevron that tells which it is, as the IDE's trees do. */
@Composable
private fun Header(isOpen: Boolean, onClick: () -> Unit, content: @Composable RowScope.() -> Unit) {
    DisableSelection {
        Row(Modifier.clickable(onClick = onClick).pointerHoverIcon(PointerIcon.Hand), verticalAlignment = Alignment.CenterVertically) {
            Icon(if (isOpen) AllIconsKeys.General.ChevronDown else AllIconsKeys.General.ChevronRight, contentDescription = null, Modifier.padding(end = 4.dp).size(16.dp))
            content()
        }
    }
}

/** A line across the transcript, with [text] in its middle: where the conversation was cut short for Claude. */
@Composable
private fun Divider(pane: TranscriptPane, part: ItemTextPart, text: String) {
    Row(Modifier.fillMaxWidth().padding(vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
        Box(Modifier.weight(1f).height(1.dp).background(TranscriptLook.secondaryText.copy(alpha = 0.4f)))
        DecoratedText(pane, part, text, JewelTheme.defaultTextStyle.copy(color = TranscriptLook.secondaryText), Modifier.padding(horizontal = 8.dp))
        Box(Modifier.weight(1f).height(1.dp).background(TranscriptLook.secondaryText.copy(alpha = 0.4f)))
    }
}

/**
 * [text], the text [part] of its item, with what the pane adds to it: Find's highlights and the console filters' links.
 * When the pane asks to show a place Find found in it, it scrolls the transcript so that the place is on the screen.
 */
@Composable
private fun DecoratedText(pane: TranscriptPane, part: ItemTextPart, text: String, style: TextStyle, modifier: Modifier = Modifier) {
    val decorations = pane.decorationsOf(part)
    val shown = remember(text, decorations) { decorations.on(AnnotatedString(text), pane::follow) }
    val requester = remember { BringIntoViewRequester() }
    var layout by remember { mutableStateOf<TextLayoutResult?>(null) }
    val toShow = pane.placeToShow?.takeIf { it.part == part }
    LaunchedEffect(toShow, layout) {
        val shownLayout = layout ?: return@LaunchedEffect
        val place = toShow ?: return@LaunchedEffect
        if (place.range.first < shownLayout.layoutInput.text.length) requester.bringIntoView(shownLayout.getBoundingBox(place.range.first).inflate(40f))
        pane.shown(place)
    }
    Text(shown, modifier.bringIntoViewRequester(requester), style = style, color = Color.Unspecified, onTextLayout = { layout = it })
}
