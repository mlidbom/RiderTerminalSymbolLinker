package dev.magnus.claudesessions

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
 * Its methods run on the EDT.
 */
class TabsParkedInTheTerminal(private val project: Project) {
    private val terminals = TerminalToolWindowTabsManager.getInstance(project)

    /** Moves [tabs] to the Terminal tool window. */
    fun park(tabs: List<ClaudeSessionTab>) {
        val terminalToolWindow = ToolWindowManager.getInstance(project).getToolWindow(TerminalToolWindowFactory.TOOL_WINDOW_ID)!!
        for (tab in tabs) {
            terminals.detachTab(tab.terminal)
            terminals.attachTab(tab.terminal, terminalToolWindow.contentManager)
        }
    }

    /**
     * Takes the parked tabs whose Claude Code runs one of [sessions] out of the Terminal tool window, and returns them by
     * the session each runs. A tab of the Terminal tool window whose Claude Code runs another session is left there.
     */
    fun takeBack(sessions: List<ClaudeSession>): Map<ClaudeSessionId, TerminalToolWindowTab> {
        val wanted = sessions.map { it.id }.toSet()
        val parked = terminals.tabs.mapNotNull { terminal ->
            val session = ClaudeSessionTab.processOf(terminal)?.let(RunningClaudeCode::inOrBelow)?.session
            if (session != null && session.id in wanted) session.id to terminal else null
        }.toMap()
        parked.values.forEach(terminals::detachTab)
        return parked
    }
}
