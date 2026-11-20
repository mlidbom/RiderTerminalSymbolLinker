package dev.magnus.claudesessions.chat.messagebox

import com.intellij.codeInsight.lookup.LookupManager
import com.intellij.openapi.Disposable
import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CustomShortcutSet
import com.intellij.openapi.command.WriteCommandAction
import com.intellij.openapi.editor.EditorFactory
import com.intellij.openapi.editor.EditorKind
import com.intellij.openapi.editor.RangeMarker
import com.intellij.openapi.editor.ScrollType
import com.intellij.openapi.editor.event.CaretEvent
import com.intellij.openapi.editor.event.CaretListener
import com.intellij.openapi.editor.event.DocumentEvent
import com.intellij.openapi.editor.event.DocumentListener
import com.intellij.openapi.editor.ex.EditorEx
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileTypes.PlainTextFileType
import com.intellij.openapi.project.DumbAwareAction
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.Disposer
import com.intellij.openapi.util.Key
import com.intellij.openapi.util.TextRange
import com.intellij.openapi.util.text.StringUtil
import com.intellij.testFramework.LightVirtualFile
import dev.magnus.claudesessions.chat.TypingFromTheChat
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.MessagePart
import dev.magnus.claudesessions.chat.claudecode.PastedImage
import dev.magnus.claudesessions.chat.claudecode.PastedText
import dev.magnus.claudesessions.chat.claudecode.TypedText
import java.awt.Image
import java.awt.datatransfer.DataFlavor
import java.awt.datatransfer.Transferable
import java.awt.event.InputEvent
import java.awt.event.KeyEvent
import java.nio.file.Path
import javax.swing.JComponent
import javax.swing.KeyStroke

/**
 * The lower part of the chat: the box the user writes a message to Claude in, an editor, so that every editing
 * feature and shortcut of the IDE works in it. Ctrl+Enter sends the message, and Enter starts a new line, as in any
 * editor, and so does Shift+Enter, as in Claude Code's own input box: Magnus, 2026-11-20, after an Enter sent a message
 * he hadn't finished. As in Claude Code's box, Escape interrupts Claude Code, and Shift+Tab changes its permission mode.
 *
 * It knows which parts of the message were pasted, so that Claude Code gets them as pasted: Ctrl+V, or any other
 * shortcut of the IDE's paste, goes through [paste], by way of the [MessageBoxPasteHandler]. An image on the clipboard,
 * with no text on it, is saved to a file by [PastedImages], and the box shows a place-holder for it, such as
 * `[Image #1]`: Claude Code attaches an image whose file's path is pasted into its box by itself.
 *
 * What it holds unsent, its [draft], is saved as it changes, and comes back as the IDE starts again: see
 * [dev.magnus.claudesessions.chat.saving.ChatKeeper].
 *
 * Its methods run on the EDT.
 */
class MessageBox(
    private val project: Project,
    private val typing: TypingFromTheChat,
    private val afterSending: (Message) -> Unit,
    parent: Disposable,
) : Disposable {
    private val file = LightVirtualFile("Message to Claude.txt", PlainTextFileType.INSTANCE, "")
    private val document = FileDocumentManager.getInstance().getDocument(file)!!

    /** The editor the message is written in. */
    val editor: EditorEx = (EditorFactory.getInstance().createEditor(document, project, file, false, EditorKind.MAIN_EDITOR) as EditorEx).apply {
        settings.isUseSoftWraps = true
        settings.isLineNumbersShown = false
        settings.isLineMarkerAreaShown = false
        settings.isFoldingOutlineShown = false
        settings.isIndentGuidesShown = false
        settings.isRightMarginShown = false
        settings.isCaretRowShown = false
        settings.additionalLinesCount = 0
        settings.isAdditionalPageAtBottom = false
        setPlaceholder("Message Claude: Ctrl+Enter sends it")
        setShowPlaceholderWhenFocused(true)
        putUserData(KEY, this@MessageBox)
    }

    val component: JComponent get() = editor.component

    /** The text pasted into the box since it was last sent, each paste with whatever the user changed in it since. */
    private val pastedTexts = mutableListOf<RangeMarker>()

    /** The images pasted into the box since it was last sent: each one's file, by where its place-holder is. */
    private val pastedImages = LinkedHashMap<RangeMarker, Path>()

    init {
        Disposer.register(parent, this)
        MessageBoxKey(KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, InputEvent.CTRL_DOWN_MASK), notWhileALookupShows = true) { send() }.registerOn(editor.contentComponent, this)
        MessageBoxKey(KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, InputEvent.SHIFT_DOWN_MASK), notWhileALookupShows = true) { startANewLine() }.registerOn(editor.contentComponent, this)
        MessageBoxKey(KeyStroke.getKeyStroke(KeyEvent.VK_ESCAPE, 0), notWhileALookupShows = true) { typing.interrupt() }.registerOn(editor.contentComponent, this)
        MessageBoxKey(KeyStroke.getKeyStroke(KeyEvent.VK_TAB, InputEvent.SHIFT_DOWN_MASK), notWhileALookupShows = false) { typing.changeThePermissionMode() }.registerOn(editor.contentComponent, this)
    }

    /** The message's text, as the box shows it. */
    val text: String get() = document.text

    /** What the box holds, unsent, with where its caret is. */
    val draft: Draft get() = Draft(message(), editor.caretModel.offset)

    /** Has the box, which is empty, hold [draft] again, as after the IDE restarted: each part marked as it came in. */
    fun restore(draft: Draft) {
        check(document.textLength == 0) { "A draft is restored into an empty box." }
        putBack(listOf(draft.message).filter { it.parts.isNotEmpty() })
        editor.caretModel.moveToOffset(draft.caret.coerceAtMost(document.textLength))
    }

    /** Has [action] run whenever what the box holds, or its caret, changes, until the box goes. */
    fun whenItChanges(action: () -> Unit) {
        document.addDocumentListener(object : DocumentListener {
            override fun documentChanged(event: DocumentEvent) = action()
        }, this)
        editor.caretModel.addCaretListener(object : CaretListener {
            override fun caretPositionChanged(event: CaretEvent) = action()
        }, this)
    }

    /**
     * Pastes [contents], a clipboard's, at the caret, in place of what is selected: its text, marked as pasted, or else
     * its image, saved to a file, as a place-holder. Anything else on a clipboard isn't pasted.
     */
    fun paste(contents: Transferable?) {
        if (contents == null) return
        when {
            contents.isDataFlavorSupported(DataFlavor.stringFlavor) -> pastedTexts += insertAtTheCaret(StringUtil.convertLineSeparators(contents.getTransferData(DataFlavor.stringFlavor) as String))
            contents.isDataFlavorSupported(DataFlavor.imageFlavor) -> {
                val file = PastedImages.save(contents.getTransferData(DataFlavor.imageFlavor) as Image)
                pastedImages[insertAtTheCaret("[Image #${pastedImages.size + 1}]")] = file
            }
        }
    }

    /**
     * Sends the message in the box to Claude Code, through [typing], unless the box is empty, and empties it at once,
     * for the next: a message Claude Code doesn't get comes back, through [putBack].
     */
    fun send() {
        if (document.text.isBlank()) return
        val message = message()
        WriteCommandAction.runWriteCommandAction(project) { document.setText("") }
        (pastedTexts + pastedImages.keys).forEach(RangeMarker::dispose)
        pastedTexts.clear()
        pastedImages.clear()
        typing.send(message)
        afterSending(message)
    }

    /**
     * Puts [messages], which Claude Code didn't get, back into the box, ahead of what the user wrote since, each on a
     * line of its own, and each part marked as what it came into the box as, as before they were sent.
     */
    fun putBack(messages: List<Message>) {
        if (messages.isEmpty()) return
        val wasEmpty = document.textLength == 0
        val lineBreak = TypedText("\n")
        val parts = messages.flatMapIndexed { at, message -> if (at == 0) message.parts else listOf(lineBreak) + message.parts } + listOfNotNull(lineBreak.takeUnless { wasEmpty })
        var offset = 0
        WriteCommandAction.runWriteCommandAction(project) {
            for (part in parts) {
                document.insertString(offset, part.text)
                markAsWhatItCameInAs(part, TextRange(offset, offset + part.text.length))
                offset += part.text.length
            }
            if (wasEmpty) editor.caretModel.moveToOffset(document.textLength)
        }
    }

    /** Starts a new line at the caret, as Enter does in an editor, in place of what is selected. */
    fun startANewLine() {
        insertAtTheCaret("\n")
    }

    /**
     * The message in the box, in parts: each stretch of its text as what it came into the box as, typed, pasted or an
     * image's place-holder, as the innermost paste around it says, the latest when pastes nest. Two pastes side by side
     * stay two parts, as in Claude Code's own box. An image whose place-holder the user deleted is left out with it.
     */
    private fun message(): Message {
        val pastes = (pastedTexts + pastedImages.keys).filter { it.isValid && !it.textRange.isEmpty }
        val cuts = (listOf(0, document.textLength) + pastes.flatMap { listOf(it.startOffset, it.endOffset) }).toSortedSet()
        val stretches = mutableListOf<Pair<RangeMarker?, TextRange>>()
        for ((start, end) in cuts.zipWithNext()) {
            val stretch = TextRange(start, end)
            val paste = pastes.lastOrNull { it.textRange.contains(stretch) }
            val last = stretches.lastOrNull()
            if (last != null && last.first == paste) stretches[stretches.lastIndex] = paste to last.second.union(stretch) else stretches += paste to stretch
        }
        return Message(stretches.map { (paste, stretch) -> partOf(paste, document.getText(stretch)) })
    }

    /** The part [text] is of a message, as the box got it: typed when [paste] is null, else pasted, or an image. */
    private fun partOf(paste: RangeMarker?, text: String): MessagePart = when (paste) {
        null -> TypedText(text)
        in pastedImages -> PastedImage(pastedImages.getValue(paste), text)
        else -> PastedText(text)
    }

    /** Marks [range] of the box as what [part] came into it as: pasted text, or an image's place-holder. */
    private fun markAsWhatItCameInAs(part: MessagePart, range: TextRange) {
        when (part) {
            is TypedText -> {}
            is PastedText -> pastedTexts += document.createRangeMarker(range)
            is PastedImage -> pastedImages[document.createRangeMarker(range)] = part.file
        }
    }

    /** Inserts [text] at the caret, in place of what is selected, and returns where it now is. */
    private fun insertAtTheCaret(text: String): RangeMarker {
        val caret = editor.caretModel.primaryCaret
        var start = 0
        WriteCommandAction.runWriteCommandAction(project) {
            start = caret.selectionStart
            document.replaceString(start, caret.selectionEnd, text)
            caret.removeSelection()
            caret.moveToOffset(start + text.length)
        }
        editor.scrollingModel.scrollToCaret(ScrollType.MAKE_VISIBLE)
        return document.createRangeMarker(start, start + text.length)
    }

    override fun dispose() {
        EditorFactory.getInstance().releaseEditor(editor)
    }

    /**
     * A key of the box's own: [perform] when the box has [keyStroke], ahead of the keymap's action for it. When
     * [notWhileALookupShows], the key goes to a completion list shown in the box instead, as Enter and Escape do in it.
     */
    private inner class MessageBoxKey(private val keyStroke: KeyStroke, private val notWhileALookupShows: Boolean, private val perform: () -> Unit) : DumbAwareAction() {
        override fun getActionUpdateThread() = ActionUpdateThread.EDT

        override fun update(event: AnActionEvent) {
            event.presentation.isEnabled = !notWhileALookupShows || LookupManager.getActiveLookup(editor) == null
        }

        override fun actionPerformed(event: AnActionEvent) = perform()

        fun registerOn(component: JComponent, parent: Disposable) {
            registerCustomShortcutSet(CustomShortcutSet(keyStroke), component, parent)
        }
    }

    companion object {
        /** The [MessageBox] whose editor an editor is, as its user data. */
        val KEY: Key<MessageBox> = Key.create("dev.magnus.claudesessions.chat.messagebox.MessageBox")
    }
}
