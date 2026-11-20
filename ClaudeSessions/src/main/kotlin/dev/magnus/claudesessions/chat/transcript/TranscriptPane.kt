package dev.magnus.claudesessions.chat.transcript

import androidx.compose.foundation.gestures.scrollBy
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import com.intellij.ide.BrowserUtil
import com.intellij.openapi.Disposable
import com.intellij.openapi.actionSystem.DataKey
import com.intellij.openapi.actionSystem.DataSink
import com.intellij.openapi.actionSystem.UiDataProvider
import com.intellij.openapi.application.EDT
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.Disposer
import com.intellij.openapi.wm.IdeFocusManager
import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.chat.claudecode.conversation.ConversationItem
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.transcript.find.FoundText
import dev.magnus.claudesessions.chat.transcript.find.TranscriptFind
import dev.magnus.claudesessions.chat.transcript.find.TranscriptFindBar
import dev.magnus.claudesessions.chat.transcript.links.TranscriptLink
import dev.magnus.claudesessions.chat.transcript.links.TranscriptLinks
import java.awt.BorderLayout
import javax.swing.JComponent
import javax.swing.JPanel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.drop
import kotlinx.coroutines.launch
import org.jetbrains.jewel.bridge.JewelComposePanel
import org.jetbrains.jewel.foundation.enableNewSwingCompositing

/**
 * The upper part of the chat: the conversation, rendered as the desktop app renders it, in Compose, with Jewel's
 * markdown renderer, styled as the IDE is. What the user said shows shaded; what Claude said as rendered markdown;
 * Claude's thinking dimmed; and each tool call as one line, its header, which opens to what it ran and got. Tool calls
 * Claude made one after the other show as one line too, which counts them, such as `Ran 2 commands, read 1 file`, and
 * opens to their lines: see [TranscriptRow].
 *
 * Find seeks text in it as in an editor, with the IDE's Find shortcuts: see [TranscriptFind] and [TranscriptFindBar].
 * The console filters link what they link in a terminal, as file paths, and in Rider the symbol linker's symbols: see
 * [TranscriptLinks].
 *
 * It follows the end of the conversation while it's scrolled to the end, and what the user scrolled to and opened stays
 * as the conversation changes. Where the user is in it, its [position], is saved as it changes, and comes back as the
 * IDE starts again: see [restore].
 *
 * Magnus asked for the rendered markdown on 2026-11-20, in place of the editor that showed the markdown as it's
 * written, "similar to how the desktop app renders the transcript. But with the option to collapse more stuff
 * automatically", and Find with it.
 *
 * Its methods run on the EDT.
 */
class TranscriptPane(val project: Project, parent: Disposable) : Disposable {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.EDT)

    /** The texts the pane shows of each item, and Claude's texts' markdown. */
    val texts = TranscriptTexts()

    /** Where the conversation is scrolled to. */
    val listState = LazyListState()

    private var items: List<ConversationItem> by mutableStateOf(emptyList())

    /** The keys of the rows the user opened: tool calls, and runs of them. */
    private var opened: Set<String> by mutableStateOf(emptySet())

    /** The rows shown, in their order. */
    val rows: List<TranscriptRow> by derivedStateOf { TranscriptRow.of(items, opened) }

    private val links = TranscriptLinks(project, scope)

    val find = TranscriptFind({ texts.of(items) }, ::showWhatFindFound)

    /** The place Find found that the text holding it is to scroll onto the screen, until it has; or null. */
    var placeToShow: FoundText? by mutableStateOf(null)
        private set

    val findBar = TranscriptFindBar(find, whereTheUserIs = { itemAtTheTop }, close = ::closeFind)

    /** Where to scroll to once the pane shows its row, as [restore] was told, or null when nowhere. */
    private var positionToScrollTo: TranscriptPosition? = null

    /** The conversation, in Compose. */
    val conversation: JComponent = run {
        // Compose in a window that holds Swing too needs Swing's newer compositing, which panels made since take.
        enableNewSwingCompositing()
        JewelComposePanel { TranscriptView(this) }
    }

    /** The pane, with the find bar above the conversation. It tells the actions of Find which pane they're in. */
    val component: JComponent = object : JPanel(BorderLayout()), UiDataProvider {
        override fun uiDataSnapshot(sink: DataSink) {
            sink[KEY] = this@TranscriptPane
        }
    }.apply {
        add(findBar.component, BorderLayout.NORTH)
        add(conversation, BorderLayout.CENTER)
    }

    init {
        Disposer.register(parent, this)
    }

    /** The id of the first item of the row at the top of the pane, or null when it shows none. */
    val itemAtTheTop: String? get() = rows.getOrNull(listState.firstVisibleItemIndex)?.items?.first()?.id

    /** The texts the console filters linked, item by item, in the conversation's order. For the development observation channel. */
    val linkedTexts: List<String> get() = items.flatMap { item -> links.of(item.id).map { it.text } }

    /**
     * Where the user is in the conversation: see [TranscriptPosition]. Until the pane has shown the position it was to
     * [restore], that one.
     */
    val position: TranscriptPosition
        get() {
            positionToScrollTo?.let { return it.copy(opened = opened) }
            val top = rows.getOrNull(listState.firstVisibleItemIndex)?.takeUnless { isAtTheEnd() } ?: return TranscriptPosition(topRow = null, pixelsIntoIt = 0, opened)
            return TranscriptPosition(top.key, listState.firstVisibleItemScrollOffset, opened)
        }

    /** Has the pane show the conversation where [position] says, as the user left it before the IDE restarted, once it shows its row. */
    fun restore(position: TranscriptPosition) {
        opened = position.opened
        positionToScrollTo = position.takeIf { it.topRow != null }
        scrollToThePositionRestored()
    }

    /** Has [action] run whenever the pane is scrolled, or a row opened or closed, until the pane goes. */
    fun whenItChanges(action: () -> Unit) {
        scope.launch {
            snapshotFlow { Triple(listState.firstVisibleItemIndex, listState.firstVisibleItemScrollOffset, opened) }.drop(1).collect { action() }
        }
    }

    /**
     * Shows [conversation] in place of the one shown. What the user opened stays open, and a run of tool calls that
     * grows out of a call the user opened opens too.
     */
    fun show(conversation: Conversation) {
        if (conversation.items == items) return
        val wasAtTheEnd = isAtTheEnd()
        val runsBefore = rows.filterIsInstance<TranscriptRow.OfAToolCallRun>().map { it.key }.toSet()
        items = conversation.items
        openTheNewRunsOfOpenCalls(runsBefore)
        texts.keepOnly(items)
        links.findIn(items.associate { it.id to texts.of(it) })
        if (find.query.isNotEmpty()) {
            find.searchAgain()
            findBar.update()
        }
        if (positionToScrollTo != null) scrollToThePositionRestored() else if (wasAtTheEnd) scrollToTheEnd()
    }

    /** Opens the row with the key [key], or closes it when it's open, as clicking its line does. */
    fun toggle(key: String) {
        opened = if (key in opened) opened - key else opened + key
    }

    /** Scrolls the pane so that the row of the item [item] is at its top. */
    fun showAtTheTop(item: String) {
        val index = rows.indexOfFirst { row -> row.items.any { it.id == item } }
        check(index >= 0) { "The transcript shows no item $item" }
        scope.launch { listState.scrollToItem(index) }
    }

    /** What the pane adds to the text [part] as it shows it: Find's highlights, and the console filters' links. */
    fun decorationsOf(part: ItemTextPart): TextDecorations = TextDecorations(find.highlightsIn(part), links.of(part.item))

    /** Follows [link], as the console that found it would. */
    fun follow(link: TranscriptLink) {
        link.info.navigate(project)
    }

    /** Opens [url], a link of Claude's markdown, in the browser. */
    fun browse(url: String) {
        BrowserUtil.browse(url)
    }

    /** Opens the find bar, with the focus in its field. */
    fun openFind() {
        findBar.open()
    }

    /** Closes the find bar, and has Find seek nothing, with the focus back in the conversation. */
    fun closeFind() {
        find.stop()
        placeToShow = null
        findBar.hide()
        IdeFocusManager.getInstance(project).requestFocus(conversation, true)
    }

    /** Has the text that holds [place] know it's on the screen, once it has scrolled it there. */
    fun shown(place: FoundText) {
        if (placeToShow == place) placeToShow = null
    }

    /**
     * Shows the place Find found, [place]: opens what hides it, a run of calls or a tool call's body, and scrolls its row
     * onto the screen, and its text then scrolls the place itself onto it, but Claude's markdown, whose renderer can't.
     */
    private fun showWhatFindFound(place: FoundText) {
        val item = items.first { it.id == place.part.item }
        if (item is ToolCall) {
            rows.filterIsInstance<TranscriptRow.OfAToolCallRun>().firstOrNull { item in it.run.calls }?.let { opened = opened + it.key }
            if (place.part.index == TranscriptTexts.BODY) opened = opened + item.id
        }
        val index = rows.indexOfFirst { it.key == item.id }
        if (!isWhollyOnTheScreen(index)) scope.launch { listState.scrollToItem(index) }
        placeToShow = place.takeUnless { item is AssistantText }
    }

    private fun isWhollyOnTheScreen(index: Int): Boolean {
        val layout = listState.layoutInfo
        return layout.visibleItemsInfo.any { it.index == index && it.offset >= layout.viewportStartOffset && it.offset + it.size <= layout.viewportEndOffset }
    }

    /** Opens each run of tool calls that wasn't one of [runsBefore], and holds a call the user opened while it was alone. */
    private fun openTheNewRunsOfOpenCalls(runsBefore: Set<String>) {
        val runsToOpen = rows.filterIsInstance<TranscriptRow.OfAToolCallRun>().filter { run -> run.key !in runsBefore && run.run.calls.any { it.id in opened } }
        if (runsToOpen.isNotEmpty()) opened = opened + runsToOpen.map { it.key }
    }

    /** Scrolls to the position [restore] was told of, once the pane shows its row. */
    private fun scrollToThePositionRestored() {
        val position = positionToScrollTo ?: return
        val index = rows.indexOfFirst { it.key == position.topRow }.takeIf { it >= 0 } ?: rows.indexOfFirst { row -> row.items.any { it.id == position.topRow } }
        if (index < 0) return
        positionToScrollTo = null
        scope.launch { listState.scrollToItem(index, position.pixelsIntoIt) }
    }

    private fun scrollToTheEnd() {
        if (rows.isEmpty()) return
        scope.launch {
            listState.scrollToItem(rows.lastIndex)
            listState.scrollBy(Float.MAX_VALUE)
        }
    }

    private fun isAtTheEnd(): Boolean = rows.isEmpty() || !listState.canScrollForward

    override fun dispose() {
        scope.cancel()
    }

    companion object {
        /** The pane an action of Find runs in, as the pane's component tells. */
        val KEY: DataKey<TranscriptPane> = DataKey.create("ClaudeSessions.TranscriptPane")
    }
}
