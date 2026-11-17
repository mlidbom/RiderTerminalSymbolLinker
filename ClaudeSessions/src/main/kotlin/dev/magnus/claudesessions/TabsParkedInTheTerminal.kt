package dev.magnus.claudesessions

import com.intellij.openapi.diagnostic.thisLogger
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.ToolWindowManager
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTab
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTabsManager
import org.jetbrains.plugins.terminal.TerminalToolWindowFactory

/**
 * Where the [ClaudeSessionsView]'s tabs wait while this plugin reloads, as for an update: the terminal plugin's own
 * Terminal tool window.
 *
 * Unloading the plugin removes its tool window, and with it the terminals of its tabs, which would end their Claude
 * Code sessions in the middle of whatever they were doing. A tab moved to the Terminal tool window first runs on, and
 * the reloaded plugin takes it back. Should the plugin not come back, as when it's uninstalled, the sessions run on in
 * the Terminal tool window.
 *
 * Every tab is parked, marked as this plugin's by a client property of its component, which holds the session the tab
 * ran last: a property named by a string reaches the reloaded plugin, whose classes are new. So the reloaded plugin
 * takes back every tab as it was, whether or not Claude Code runs in it, and whatever Claude Code's record of its
 * session says as it reads it. Told by the session their Claude Code's record names, one tab in use was lost at every
 * reload, for a reason not found, and a tab whose Claude Code had exited went too (2026-11-17).
 * Tabs that an earlier build parked carry no mark: those are told by their session still.
 *
 * A tab moves with the terminal's `detachTab`, and the content manager's `addContent`, not the terminal's `attachTab`:
 * that activates the Terminal tool window, which shares the bottom of the IDE with this plugin's and so hides it, and
 * selects the tab only once the Terminal tool window is active, which undid the reloaded view's selection of the tab
 * that was selected before.
 *
 * What it parks and takes back goes to the IDE's log, so that a tab lost in a reload can be traced.
 *
 * Its methods run on the EDT.
 */
class TabsParkedInTheTerminal(private val project: Project) {
    private val terminals = TerminalToolWindowTabsManager.getInstance(project)

    /** Moves [tabs] to the Terminal tool window, each marked with the session it ran last. */
    fun park(tabs: List<ClaudeSessionTab>) {
        //bug: with tabs parked, the unloaded build stays in memory: the IDE logs "Plugins that were expected to unload but
        //   are still referenced", and tells the user "'Claude Sessions' plugin didn't unload fully". It has done so at
        //   every reload since the first build. With no tab open, the unload is clean (2026-11-17), so something in a
        //   parked tab, which outlives the unload, references the old build. What isn't found: the terminal keeps no
        //   reference to the content manager a tab was created with, and the platform's snapshot of a failed unload
        //   (registry ide.plugins.snapshot.on.unload.fail) wrote nothing in Rider. Next: the live heap, with a debugger.
        val terminalToolWindow = ToolWindowManager.getInstance(project).getToolWindow(TerminalToolWindowFactory.TOOL_WINDOW_ID)!!
        for (tab in tabs) {
            tab.terminal.content.component.putClientProperty(PARKED_TAB, tab.session?.id?.value.orEmpty())
            terminals.detachTab(tab.terminal)
            terminalToolWindow.contentManager.addContent(tab.terminal.content)
        }
        thisLogger().info("Parked ${tabs.size} tabs of ${project.name} in the Terminal tool window: ${tabs.joinToString { describe(it.terminal, it.session) }}")
    }

    /**
     * Takes the parked tabs out of the Terminal tool window, and returns them in their order, each with the one of the
     * [saved] sessions it ran last, if it has one. A tab an earlier build parked is taken back when its Claude Code runs
     * one of them; the other tabs of the Terminal tool window are left there.
     */
    fun takeBack(saved: List<ClaudeSession>): List<ParkedTab> {
        val savedById = saved.associateBy { it.id }
        val parked = terminals.tabs.mapNotNull { terminal ->
            val mark = terminal.content.component.getClientProperty(PARKED_TAB) as String?
            if (mark != null) ParkedTab(terminal, savedById[ClaudeSessionId(mark)])
            else ClaudeSessionTab.processOf(terminal)?.let(RunningClaudeCode::inOrBelow)?.session?.id?.let(savedById::get)?.let { ParkedTab(terminal, it) }
        }
        for (tab in parked) {
            terminals.detachTab(tab.terminal)
            tab.terminal.content.component.putClientProperty(PARKED_TAB, null)
        }
        thisLogger().info("Took back ${parked.size} tabs of ${project.name} from the Terminal tool window: ${parked.joinToString { describe(it.terminal, it.session) }}")
        return parked
    }

    private fun describe(terminal: TerminalToolWindowTab, session: ClaudeSession?): String =
        "'${terminal.content.displayName}' (process ${ClaudeSessionTab.processOf(terminal)?.pid()}, session ${session?.id?.value})"

    /** A tab that waited in the Terminal tool window, with the session it ran last, if it had one. */
    data class ParkedTab(val terminal: TerminalToolWindowTab, val session: ClaudeSession?)

    private companion object {
        /** The client property that marks a parked tab as this plugin's, with its session's id, or "" for none. */
        const val PARKED_TAB = "dev.magnus.claudesessions.parkedTab"
    }
}
