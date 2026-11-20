package dev.magnus.claudesessions.chat.transcript.find

import com.intellij.icons.AllIcons
import com.intellij.openapi.actionSystem.ActionManager
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CustomShortcutSet
import com.intellij.openapi.actionSystem.DefaultActionGroup
import com.intellij.openapi.project.DumbAwareAction
import com.intellij.openapi.wm.IdeFocusManager
import com.intellij.ui.DocumentAdapter
import com.intellij.ui.SearchTextField
import com.intellij.ui.components.JBLabel
import com.intellij.util.ui.JBUI
import java.awt.BorderLayout
import java.awt.event.InputEvent
import java.awt.event.KeyEvent
import javax.swing.JComponent
import javax.swing.JPanel
import javax.swing.KeyStroke
import javax.swing.event.DocumentEvent
import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/**
 * The bar above the [TranscriptPane]'s conversation in which the user seeks a text, as in an editor's Find: the field,
 * how many places hold the text and which of them the user is at, and buttons to go to the one before and the next, and
 * to close the bar. In the field, Enter goes to the next place, Shift+Enter to the one before, and Escape closes the bar.
 *
 * Hidden until [open]. Its methods run on the EDT.
 *
 * @param whereTheUserIs The id of the item at the top of the pane: Find seeks from it on.
 */
class TranscriptFindBar(private val find: TranscriptFind, private val whereTheUserIs: () -> String?, private val close: () -> Unit) {
    private val searchField = SearchTextField(false)

    private val count = JBLabel()

    private val buttons = ActionManager.getInstance().createActionToolbar(
        "ClaudeSessions.TranscriptFind",
        DefaultActionGroup(
            FindBarAction("Previous Occurrence", AllIcons.Actions.PreviousOccurence) { find.previous() },
            FindBarAction("Next Occurrence", AllIcons.Actions.NextOccurence) { find.next() },
            FindBarAction("Close", AllIcons.Actions.Close) { close() },
        ),
        true,
    ).apply { targetComponent = searchField }

    val component: JComponent = JPanel(BorderLayout(JBUI.scale(6), 0)).apply {
        isVisible = false
        border = JBUI.Borders.compound(JBUI.Borders.customLineBottom(JBUI.CurrentTheme.ToolWindow.borderColor()), JBUI.Borders.empty(2, 6))
        add(searchField, BorderLayout.CENTER)
        add(JPanel(BorderLayout(JBUI.scale(6), 0)).apply {
            isOpaque = false
            add(count, BorderLayout.WEST)
            add(buttons.component, BorderLayout.EAST)
        }, BorderLayout.EAST)
    }

    /** What the user seeks, as in the field. */
    val text: String get() = searchField.text

    /** How many places hold it, and which the user is at, as the bar says it, such as `3/17`. */
    val countSaid: String get() = count.text

    init {
        searchField.addDocumentListener(object : DocumentAdapter() {
            override fun textChanged(event: DocumentEvent) {
                find.seek(searchField.text, whereTheUserIs())
                update()
            }
        })
        FindBarAction("Next Occurrence", null) { find.next() }.registerCustomShortcutSet(CustomShortcutSet(KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, 0)), searchField.textEditor)
        FindBarAction("Previous Occurrence", null) { find.previous() }.registerCustomShortcutSet(CustomShortcutSet(KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, InputEvent.SHIFT_DOWN_MASK)), searchField.textEditor)
        FindBarAction("Close", null) { close() }.registerCustomShortcutSet(CustomShortcutSet(KeyStroke.getKeyStroke(KeyEvent.VK_ESCAPE, 0)), searchField.textEditor)
    }

    /** Shows the bar, with the focus in the field and its text selected, so that what the user types next replaces it. */
    fun open() {
        component.isVisible = true
        searchField.selectText()
        IdeFocusManager.getGlobalInstance().requestFocus(searchField.textEditor, true)
    }

    /** Hides the bar. */
    fun hide() {
        component.isVisible = false
    }

    /** Has the field hold [text], as when the user types it. */
    fun type(text: String) {
        searchField.text = text
    }

    /** Says anew how many places hold the text, and which the user is at, as Find changed. */
    fun update() {
        val current = find.current
        count.text = when {
            find.query.isEmpty() -> ""
            current == null -> "0 results"
            else -> "${current + 1}/${find.found.size}"
        }
    }

    /** A button or key of the bar, which does [perform] and says anew where Find is. */
    private inner class FindBarAction(text: String, icon: javax.swing.Icon?, private val perform: () -> Unit) : DumbAwareAction(text, null, icon) {
        override fun actionPerformed(event: AnActionEvent) {
            perform()
            update()
        }
    }
}
