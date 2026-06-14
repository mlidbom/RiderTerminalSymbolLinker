package dev.magnus.csl

import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.popup.PopupChooserBuilder
import com.intellij.ui.ColoredListCellRenderer
import com.intellij.ui.JBColor
import com.intellij.ui.SimpleTextAttributes
import com.intellij.ui.components.JBList
import java.awt.Color
import java.awt.Toolkit
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import javax.swing.JList
import javax.swing.ListSelectionModel

/**
 * The disambiguation popup shared by symbol and file links: a searchable list of navigation targets,
 * styled and filtered by a single label per item. A plain click (or Enter) opens the target and closes
 * the list; a Ctrl-click (Cmd-click on macOS) opens it in the background and leaves the list open, so
 * several candidates can be opened in turn before dismissing it.
 *
 * The builder's own click handler ignores menu-shortcut-modified clicks (it treats them as a selection
 * toggle, not an activation), so it won't close the popup — we just open the row under the cursor and
 * keep focus on the list.
 *
 * Rows for which [isEmphasized] is true are marked as already open in the IDE: a tinted background band
 * plus an "(open)" tag. The tag carries the mark through selection too, where the selection background
 * replaces the tint. [isEmphasized] is evaluated on every paint, and a Ctrl-click background open
 * repaints the list — so opening a candidate from the picker immediately marks it open.
 *
 * @param open invoked with the chosen item and whether to request focus (false = background open).
 */
object LinkPicker {
    /** Row tint for files already open in the IDE — clearly visible, with light/dark variants. */
    private val OPEN_FILE_BACKGROUND = JBColor(Color(0xBE, 0xE6, 0xBE), Color(0x33, 0x4D, 0x33))

    fun <T> show(
        project: Project,
        title: String,
        items: List<T>,
        label: (T) -> String,
        isEmphasized: (T) -> Boolean = { false },
        open: (item: T, requestFocus: Boolean) -> Unit,
    ) {
        val list = JBList(items).apply { selectionMode = ListSelectionModel.SINGLE_SELECTION }
        list.addMouseListener(object : MouseAdapter() {
            override fun mouseClicked(e: MouseEvent) {
                val keepOpenModifier = Toolkit.getDefaultToolkit().menuShortcutKeyMaskEx
                if (e.button != MouseEvent.BUTTON1 || e.modifiersEx and keepOpenModifier == 0) return
                val index = list.locationToIndex(e.point)
                if (index < 0 || !list.getCellBounds(index, index).contains(e.point)) return
                open(list.model.getElementAt(index), false)
                list.repaint() // the just-opened file should now show its "open" highlight
                e.consume()
            }
        })

        PopupChooserBuilder(list)
            .setTitle(title)
            .setRenderer(
                object : ColoredListCellRenderer<T>() {
                    override fun customizeCellRenderer(
                        list: JList<out T>,
                        value: T,
                        index: Int,
                        selected: Boolean,
                        hasFocus: Boolean,
                    ) {
                        append(label(value))
                        if (isEmphasized(value)) {
                            append("   (open)", SimpleTextAttributes.REGULAR_BOLD_ATTRIBUTES)
                            if (!selected) background = OPEN_FILE_BACKGROUND
                        }
                    }
                },
            )
            .setItemChosenCallback { open(it, true) }
            // Always-visible search field on top, like Go to Symbol; filters on the visible row text.
            .setNamerForFiltering { label(it) }
            .setFilterAlwaysVisible(true)
            .createPopup()
            .showCenteredInCurrentWindow(project)
    }
}
