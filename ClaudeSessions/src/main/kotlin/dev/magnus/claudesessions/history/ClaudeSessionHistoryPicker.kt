package dev.magnus.claudesessions.history

import com.intellij.icons.AllIcons
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.openapi.progress.Task
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.popup.JBPopup
import com.intellij.openapi.ui.popup.JBPopupFactory
import com.intellij.openapi.ui.popup.JBPopupListener
import com.intellij.openapi.ui.popup.LightweightWindowEvent
import com.intellij.openapi.util.IconLoader
import com.intellij.ui.CollectionListModel
import com.intellij.ui.ColoredListCellRenderer
import com.intellij.ui.DocumentAdapter
import com.intellij.ui.DoubleClickListener
import com.intellij.ui.JBColor
import com.intellij.ui.SearchTextField
import com.intellij.ui.SimpleTextAttributes
import com.intellij.ui.components.JBList
import com.intellij.ui.components.JBRadioButton
import com.intellij.ui.components.JBScrollPane
import com.intellij.util.text.DateFormatUtil
import com.intellij.util.ui.EmptyIcon
import com.intellij.util.ui.JBUI
import dev.magnus.claudesessions.ClaudeSessionsView
import java.awt.BorderLayout
import java.awt.Color
import java.awt.FlowLayout
import java.awt.event.KeyAdapter
import java.awt.event.KeyEvent
import java.awt.event.MouseEvent
import java.util.concurrent.ConcurrentHashMap
import javax.swing.ButtonGroup
import javax.swing.Icon
import javax.swing.JList
import javax.swing.JPanel
import javax.swing.ListSelectionModel
import javax.swing.event.DocumentEvent

/**
 * The session history picker: a popup that lists the [ClaudeSessionHistory], most recently active first, for the user
 * to resume a session from. Typing filters it, and its [ClaudeSessionHistoryScope]s widen it from the project's worktree
 * to every project. Enter, or a double click, chooses the selected session, which does what [WhereTheSessionIs] says:
 * resumes it in a tab of the project's [ClaudeSessionsView], or brings forward the tab it runs in. A session that runs
 * outside Claude Sessions' tabs is listed greyed out, and can't be chosen.
 *
 * Its members run on the EDT.
 */
class ClaudeSessionHistoryPicker private constructor(private val project: Project, private val history: ClaudeSessionHistory) {
    private val search = SearchTextField(false)

    private val list = JBList<ClaudeSessionHistoryEntry>().apply {
        cellRenderer = Renderer()
        selectionMode = ListSelectionModel.SINGLE_SELECTION
        emptyText.text = "No sessions"
    }

    private val scopeButtons: Map<ClaudeSessionHistoryScope, JBRadioButton> = history.worktrees.scopes.associateWith { JBRadioButton(it.label) }

    private val popup: JBPopup

    /** The scope the picker lists: [ClaudeSessionHistoryScope.THIS_WORKTREE] as it opens. */
    var scope: ClaudeSessionHistoryScope = ClaudeSessionHistoryScope.THIS_WORKTREE
        private set

    /** The scopes the picker offers: all worktrees only for a project in git. */
    val scopes: List<ClaudeSessionHistoryScope> get() = history.worktrees.scopes

    /** What is typed into the search field, which filters the entries. */
    var filter: String
        get() = search.text
        set(value) {
            search.text = value
        }

    /** The entries the picker lists, in their order. */
    val listed: List<ClaudeSessionHistoryEntry> get() = (list.model as CollectionListModel<ClaudeSessionHistoryEntry>).items

    /** The listed entry that is selected, by its place, or -1 when none is. */
    var selected: Int
        get() = list.selectedIndex
        set(value) {
            list.selectedIndex = value
        }

    init {
        ButtonGroup().also { group -> scopeButtons.values.forEach(group::add) }
        scopeButtons.getValue(scope).isSelected = true
        scopeButtons.forEach { (scope, button) -> button.addActionListener { list(scope) } }
        search.addDocumentListener(object : DocumentAdapter() {
            override fun textChanged(event: DocumentEvent) = refresh()
        })
        search.textEditor.addKeyListener(object : KeyAdapter() {
            override fun keyPressed(event: KeyEvent) {
                when (event.keyCode) {
                    KeyEvent.VK_DOWN -> moveTheSelection(1)
                    KeyEvent.VK_UP -> moveTheSelection(-1)
                    KeyEvent.VK_ENTER -> chooseTheSelected()
                    else -> return
                }
                event.consume()
            }
        })
        list.addKeyListener(object : KeyAdapter() {
            override fun keyPressed(event: KeyEvent) {
                if (event.keyCode != KeyEvent.VK_ENTER) return
                chooseTheSelected()
                event.consume()
            }
        })
        object : DoubleClickListener() {
            override fun onDoubleClick(event: MouseEvent): Boolean {
                val clicked = list.locationToIndex(event.point).takeIf { it >= 0 } ?: return false
                list.selectedIndex = clicked
                chooseTheSelected()
                return true
            }
        }.installOn(list)
        val panel = JPanel(BorderLayout()).apply {
            add(JPanel(BorderLayout(JBUI.scale(8), 0)).apply {
                border = JBUI.Borders.empty(4)
                add(search, BorderLayout.CENTER)
                add(JPanel(FlowLayout(FlowLayout.RIGHT, JBUI.scale(4), 0)).apply { scopeButtons.values.forEach { add(it) } }, BorderLayout.EAST)
            }, BorderLayout.NORTH)
            add(JBScrollPane(list), BorderLayout.CENTER)
            preferredSize = JBUI.size(900, 480)
        }
        refresh()
        popup = JBPopupFactory.getInstance().createComponentPopupBuilder(panel, search.textEditor)
            .setTitle("Resume a Claude Session")
            .setProject(project)
            .setMovable(true)
            .setResizable(true)
            .setRequestFocus(true)
            .setCancelOnClickOutside(true)
            .setDimensionServiceKey(project, "ClaudeSessions.HistoryPicker", true)
            .createPopup()
        popup.addListener(object : JBPopupListener {
            override fun onClosed(event: LightweightWindowEvent) {
                shown.remove(project, this@ClaudeSessionHistoryPicker)
            }
        })
    }

    /** Lists the entries [scope] covers, as a click on its button does. */
    fun list(scope: ClaudeSessionHistoryScope) {
        scopeButtons.getValue(scope).isSelected = true
        this.scope = scope
        refresh()
        search.textEditor.requestFocusInWindow()
    }

    /** Chooses the selected entry, as Enter does. */
    fun chooseTheSelected() {
        list.selectedValue?.let(::choose)
    }

    /** Closes the picker, choosing nothing, as Escape does. */
    fun close() {
        popup.cancel()
    }

    /**
     * Has the search field take the key [keyCode] as from the keyboard, as its key listeners do: for the development
     * observation channel, through which the specifications press keys in the picker.
     */
    fun press(keyCode: Int) {
        val event = KeyEvent(search.textEditor, KeyEvent.KEY_PRESSED, System.currentTimeMillis(), 0, keyCode, KeyEvent.CHAR_UNDEFINED)
        search.textEditor.keyListeners.forEach { it.keyPressed(event) }
    }

    /**
     * Double-clicks the listed entry [entry], by its place, as the mouse does: two clicks, each a press, a release and a
     * click, as the platform's click listeners count them. For the development observation channel.
     */
    fun doubleClick(entry: Int) {
        val cell = list.getCellBounds(entry, entry)!!
        for (click in 1..2) {
            for (id in listOf(MouseEvent.MOUSE_PRESSED, MouseEvent.MOUSE_RELEASED, MouseEvent.MOUSE_CLICKED)) {
                list.dispatchEvent(MouseEvent(list, id, System.currentTimeMillis(), 0, cell.x + 5, cell.y + cell.height / 2, click, false, MouseEvent.BUTTON1))
            }
        }
    }

    private fun moveTheSelection(by: Int) {
        if (list.model.size == 0) return
        list.selectedIndex = (list.selectedIndex + by).coerceIn(0, list.model.size - 1)
        list.ensureIndexIsVisible(list.selectedIndex)
    }

    /** Closes the picker and does what choosing [entry] does, unless it can't be chosen. */
    private fun choose(entry: ClaudeSessionHistoryEntry) {
        if (!entry.where.canBeChosen) return
        popup.closeOk(null)
        entry.choose(ClaudeSessionsView.openIn(project))
    }

    private fun show() {
        shown[project] = this
        popup.showCenteredInCurrentWindow(project)
    }

    private fun refresh() {
        val entries = history.listed(scope, search.text)
        list.model = CollectionListModel(entries)
        if (entries.isNotEmpty()) list.selectedIndex = 0
    }

    /**
     * Renders an entry: its title, its last prompt, when it was last active, its folder and branch when the scope is wider
     * than the project's worktree, and, marked, where it runs. One that can't be chosen is greyed out.
     */
    private inner class Renderer : ColoredListCellRenderer<ClaudeSessionHistoryEntry>() {
        override fun customizeCellRenderer(list: JList<out ClaudeSessionHistoryEntry>, entry: ClaudeSessionHistoryEntry, index: Int, selected: Boolean, hasFocus: Boolean) {
            val where = entry.where
            icon = iconOf(where)
            append(entry.title.asOneShortLine(), if (where.canBeChosen) SimpleTextAttributes.REGULAR_BOLD_ATTRIBUTES else SimpleTextAttributes.GRAYED_BOLD_ATTRIBUTES)
            entry.lastPrompt?.let { append("   ${it.asOneShortLine()}", SimpleTextAttributes.GRAYED_ATTRIBUTES) }
            append("   ${DateFormatUtil.formatPrettyDateTime(entry.lastActive.toEpochMilli())}", SimpleTextAttributes.GRAYED_SMALL_ATTRIBUTES)
            if (scope != ClaudeSessionHistoryScope.THIS_WORKTREE) {
                append("   ${entry.session.workingDirectory}${entry.gitBranch?.let { " ($it)" }.orEmpty()}", SimpleTextAttributes.GRAYED_SMALL_ATTRIBUTES)
            }
            markOf(where)?.let { (text, attributes) -> append("   $text", attributes) }
        }

        private fun iconOf(where: WhereTheSessionIs): Icon = when (where) {
            WhereTheSessionIs.NotRunning -> EmptyIcon.ICON_16
            is WhereTheSessionIs.ExitedInATab -> AllIcons.Nodes.Console
            is WhereTheSessionIs.RunningInATab, is WhereTheSessionIs.RunningInAnotherIde -> AllIcons.Actions.Execute
            is WhereTheSessionIs.RunningOutsideClaudeSessions -> IconLoader.getDisabledIcon(AllIcons.Actions.Execute)
        }

        private fun markOf(where: WhereTheSessionIs): Pair<String, SimpleTextAttributes>? = when (where) {
            WhereTheSessionIs.NotRunning -> null
            is WhereTheSessionIs.ExitedInATab -> "open in a tab${ofAnotherProject(where.view)}" to SimpleTextAttributes.GRAYED_ATTRIBUTES
            is WhereTheSessionIs.RunningInATab -> "running in a tab${ofAnotherProject(where.view)}" to RUNNING
            is WhereTheSessionIs.RunningInAnotherIde -> "running in another IDE" to RUNNING
            is WhereTheSessionIs.RunningOutsideClaudeSessions -> "running outside Claude Sessions" to SimpleTextAttributes.GRAYED_ITALIC_ATTRIBUTES
        }

        private fun ofAnotherProject(view: ClaudeSessionsView): String = if (view.project == project) "" else " of ${view.project.name}"

        /** [this] on one line, cut short where it would crowd out the rest of the entry. */
        private fun String.asOneShortLine(): String {
            val oneLine = lines().joinToString(" ").trim()
            return if (oneLine.length <= 100) oneLine else oneLine.take(99) + "…"
        }
    }

    companion object {
        /** The bold green of an entry's mark that its session runs. */
        private val RUNNING = SimpleTextAttributes(SimpleTextAttributes.STYLE_BOLD, JBColor(Color(0x2E7D32), Color(0x6FBF73)))

        private val shown = ConcurrentHashMap<Project, ClaudeSessionHistoryPicker>()

        /** The picker shown in [project]'s window now, if one is. */
        fun shownIn(project: Project): ClaudeSessionHistoryPicker? = shown[project]

        /** Reads the history in the background, and then shows the picker in [project]'s window. */
        fun open(project: Project) {
            object : Task.Backgroundable(project, "Reading the Claude Code sessions", false) {
                private lateinit var history: ClaudeSessionHistory

                override fun run(indicator: ProgressIndicator) {
                    history = ClaudeSessionHistory.readFor(project)
                }

                override fun onSuccess() {
                    ClaudeSessionHistoryPicker(project, history).show()
                }
            }.queue()
        }
    }
}
