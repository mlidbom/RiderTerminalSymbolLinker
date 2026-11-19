package dev.magnus.claudesessions.chat.transcript

import com.intellij.execution.filters.ConsoleFilterProvider
import com.intellij.execution.impl.EditorHyperlinkSupport
import com.intellij.openapi.Disposable
import com.intellij.openapi.actionSystem.IdeActions
import com.intellij.openapi.application.WriteAction
import com.intellij.openapi.command.CommandProcessor
import com.intellij.openapi.command.undo.UndoUtil
import com.intellij.openapi.editor.EditorFactory
import com.intellij.openapi.editor.EditorKind
import com.intellij.openapi.editor.FoldRegion
import com.intellij.openapi.editor.RangeMarker
import com.intellij.openapi.editor.ScrollType
import com.intellij.openapi.editor.ex.EditorEx
import com.intellij.openapi.editor.highlighter.EditorHighlighterFactory
import com.intellij.openapi.fileTypes.FileTypeManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.Disposer
import com.intellij.openapi.util.Expirable
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.chat.claudecode.conversation.ConversationItem
import javax.swing.JComponent

/**
 * The upper part of the chat: the conversation, in a read-only editor, so that the IDE's own search, selection and
 * shortcuts work in it. Its text is markdown, highlighted as markdown where the IDE has the Markdown plugin, as the
 * bundled one in most. Each tool call shows as its header, with its input and result folded away below it, collapsed
 * until the user opens it.
 *
 * Every filter on the platform's `consoleFilterProvider` extension point runs over the text, as over a console's: in
 * Rider that brings the symbol linker's links, and in every IDE the platform's own, such as those of file paths.
 *
 * When the conversation changes, it shows anew only from the first item that differs, so that what the user has
 * scrolled to, selected or opened above stays as it was: as when a tool call's result comes, or Claude says more. It
 * follows the end of the conversation while it's scrolled to the end.
 *
 * Its methods run on the EDT.
 */
class TranscriptPane(private val project: Project, parent: Disposable) : Disposable {
    private val document = EditorFactory.getInstance().createDocument("").also(UndoUtil::disableUndoFor)

    /** The editor that shows the conversation. */
    val editor: EditorEx = (EditorFactory.getInstance().createViewer(document, project, EditorKind.CONSOLE) as EditorEx).apply {
        settings.isUseSoftWraps = true
        settings.isLineNumbersShown = false
        settings.isLineMarkerAreaShown = false
        settings.isFoldingOutlineShown = true
        settings.isIndentGuidesShown = false
        settings.isCaretRowShown = false
        settings.isRightMarginShown = false
        settings.additionalLinesCount = 0
        settings.isAdditionalPageAtBottom = false
        contextMenuGroupId = IdeActions.GROUP_CONSOLE_EDITOR_POPUP
        FileTypeManager.getInstance().findFileTypeByName(MARKDOWN)?.let { highlighter = EditorHighlighterFactory.getInstance().createEditorHighlighter(project, it) }
    }

    val component: JComponent get() = editor.component

    /** The items shown, in their order, each with where its text is. */
    private val shown = mutableListOf<ShownItem>()

    /** The links over the text, from the console filters. */
    private val links = EditorHyperlinkSupport.get(editor)

    /**
     * The console filters, each run over the text by itself, as the terminal runs them: a filter's result ends the
     * filtering of its line unless it says otherwise, so a `CompositeFilter` of them all would let the symbol linker's
     * symbol links hide its file links on the same line.
     */
    private val filters = ConsoleFilterProvider.FILTER_PROVIDERS.extensionList.flatMap { it.getDefaultFilters(project).asList() }

    /**
     * How many times text has been taken away: links still being found for text taken away since are dropped, as their
     * places are no longer theirs.
     */
    private var removals = 0

    private var isDisposed = false

    init {
        Disposer.register(parent, this)
    }

    /** The conversation's text, as shown. For the development observation channel. */
    val text: String get() = document.text

    /** The folds, in their order: each tool call's. For the development observation channel. */
    val folds: List<FoldRegion> get() = shown.mapNotNull { it.fold }

    /** The texts the links cover, in their order. For the development observation channel. */
    val linkedTexts: List<String>
        get() = editor.markupModel.allHighlighters.filter { it.isValid && EditorHyperlinkSupport.getHyperlinkInfo(it) != null }.sortedBy { it.startOffset }.map { document.getText(it.textRange) }

    /** Shows [conversation] in place of the one shown: anew from the first item that differs. */
    fun show(conversation: Conversation) {
        val items = conversation.items
        val firstThatDiffers = shown.indices.firstOrNull { it >= items.size || shown[it].item != items[it] } ?: shown.size
        if (firstThatDiffers == shown.size && items.size == shown.size) return
        val wasAtTheEnd = isScrolledToTheEnd()
        val opened = shown.drop(firstThatDiffers).filter { it.fold?.isExpanded == true }.map { it.item.id }.toSet()
        removeFrom(firstThatDiffers)
        val appendedFrom = document.textLength
        append(items.drop(firstThatDiffers), after = items.getOrNull(firstThatDiffers - 1), opened)
        findLinksIn(appendedFrom)
        if (wasAtTheEnd) editor.scrollingModel.scrollTo(editor.offsetToLogicalPosition(document.textLength), ScrollType.MAKE_VISIBLE)
    }

    /** Takes the items from [first] on away, with their text, folds, and the links over it, which go with their text. */
    private fun removeFrom(first: Int) {
        if (first >= shown.size) return
        val removed = shown.subList(first, shown.size)
        val start = removed.first().range.startOffset
        removals++
        editor.foldingModel.runBatchFoldingOperation { removed.mapNotNull { it.fold }.filter { it.isValid }.forEach(editor.foldingModel::removeFoldRegion) }
        changeTheText { document.deleteString(start, document.textLength) }
        removed.forEach { it.range.dispose() }
        removed.clear()
    }

    /** Appends [items], the first shown after [after], with the folds of those whose ids are in [opened] open. */
    private fun append(items: List<ConversationItem>, after: ConversationItem?, opened: Set<String>) {
        val texts = items.zip(listOf(after) + items.dropLast(1)) { item, previous -> item to ItemText.of(item, previous) }
        changeTheText { document.insertString(document.textLength, texts.joinToString("") { it.second.text }) }
        var start = document.textLength - texts.sumOf { it.second.text.length }
        editor.foldingModel.runBatchFoldingOperation {
            for ((item, text) in texts) {
                val range = document.createRangeMarker(start, start + text.text.length)
                val fold = text.folded?.let { editor.foldingModel.addFoldRegion(start + it.first, start + it.last + 1, FOLDED) }
                fold?.isExpanded = item.id in opened
                shown += ShownItem(item, range, fold)
                start += text.text.length
            }
        }
    }

    /** Has the console filters find links in the text from [offset] to the end, in the background. */
    private fun findLinksIn(offset: Int) {
        if (offset >= document.textLength) return
        val removalsNow = removals
        val expired = Expirable { isDisposed || removals != removalsNow }
        for (filter in filters) links.highlightHyperlinksLater(filter, document.getLineNumber(offset), document.getLineNumber(document.textLength), expired)
    }

    /** Has [change] change the text, as the user can't: the pane's changes aren't for the user to undo. */
    private fun changeTheText(change: () -> Unit) {
        CommandProcessor.getInstance().runUndoTransparentAction { WriteAction.run<Throwable>(change) }
    }

    private fun isScrolledToTheEnd(): Boolean {
        if (shown.isEmpty()) return true
        val visible = editor.scrollingModel.visibleArea
        return visible.y + visible.height >= editor.contentComponent.height - editor.lineHeight
    }

    override fun dispose() {
        isDisposed = true
        EditorFactory.getInstance().releaseEditor(editor)
    }

    /** An item shown: where its text is, and its fold, if it has one. */
    private class ShownItem(val item: ConversationItem, val range: RangeMarker, val fold: FoldRegion?)

    private companion object {
        /** The name of the Markdown plugin's file type, whose highlighting the text gets where the IDE has it. */
        const val MARKDOWN = "Markdown"

        /** What a fold shows while it's collapsed, after the tool call's header. */
        const val FOLDED = " …"
    }
}
