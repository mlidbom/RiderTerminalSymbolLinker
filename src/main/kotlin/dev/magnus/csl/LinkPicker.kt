package dev.magnus.csl

import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.popup.PopupChooserBuilder
import com.intellij.ui.SimpleListCellRenderer
import com.intellij.ui.components.JBList
import java.awt.Toolkit
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
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
 * @param open invoked with the chosen item and whether to request focus (false = background open).
 */
object LinkPicker {
    fun <T> show(
        project: Project,
        title: String,
        items: List<T>,
        label: (T) -> String,
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
                e.consume()
            }
        })

        PopupChooserBuilder(list)
            .setTitle(title)
            .setRenderer(SimpleListCellRenderer.create("") { label(it) })
            .setItemChosenCallback { open(it, true) }
            // Always-visible search field on top, like Go to Symbol; filters on the visible row text.
            .setNamerForFiltering { label(it) }
            .setFilterAlwaysVisible(true)
            .createPopup()
            .showCenteredInCurrentWindow(project)
    }
}
