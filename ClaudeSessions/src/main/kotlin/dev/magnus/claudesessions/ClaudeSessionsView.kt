package dev.magnus.claudesessions

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowManager
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTab
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTabsManager
import com.intellij.ui.content.ContentManagerEvent
import com.intellij.ui.content.ContentManagerListener
import java.nio.file.Path
import java.util.concurrent.ConcurrentHashMap
import com.intellij.ui.content.Content

/**
 * A project's Claude Sessions tool window: a tab for each Claude Code session, each a [ClaudeSessionTab] whose shell
 * runs Claude Code. When the tool window opens, it resumes the sessions the [ClaudeSessionKeeper] saved, so that they
 * survive the IDE's restart.
 *
 * Its methods run on the EDT.
 */
@Service(Service.Level.PROJECT)
class ClaudeSessionsView(val project: Project) {
    private var toolWindow: ToolWindow? = null
    private val tabsByContent = ConcurrentHashMap<Content, ClaudeSessionTab>()

    /** Whether the tool window has opened: [tabs] are only there once it has. */
    val isOpen: Boolean get() = toolWindow != null

    /** The tabs, in the order the tool window shows them. */
    val tabs: List<ClaudeSessionTab>
        get() = toolWindow?.contentManager?.contents?.mapNotNull(tabsByContent::get).orEmpty()

    /** The tab the tool window shows, of those it has. */
    val selectedTab: ClaudeSessionTab?
        get() = toolWindow?.contentManager?.selectedContent?.let(tabsByContent::get)

    /**
     * Takes [toolWindow] as the view, as [ClaudeSessionsToolWindowFactory] gives it its content, and has the
     * [ClaudeSessionKeeper] start keeping its sessions. The saved sessions come back in it in their order: those
     * [TabsParkedInTheTerminal] while the plugin reloaded in the tabs that ran them all along, and the others resumed,
     * unless they can't be: archived or deleted since they ran. The tab that was selected is selected again.
     */
    fun open(toolWindow: ToolWindow) {
        this.toolWindow = toolWindow
        toolWindow.contentManager.addContentManagerListener(object : ContentManagerListener {
            override fun contentRemoved(event: ContentManagerEvent) {
                tabsByContent.remove(event.content)
            }
        })
        val saved = SavedClaudeSessions(project).read()
        val parked = TabsParkedInTheTerminal(project).takeBack(saved.sessions)
        val tabs = saved.sessions.mapNotNull { session ->
            val parkedTab = parked[session.id]
            if (parkedTab != null) adopt(parkedTab, session) else if (session.canBeResumed) resume(session) else null
        }
        tabs.firstOrNull { it.session?.id == saved.selected }?.let { toolWindow.contentManager.setSelectedContent(it.content, false) }
        ClaudeSessionKeeper.getInstance(project).startKeeping()
    }

    /**
     * Moves the tabs that run Claude Code to the Terminal tool window, where their sessions run on while the plugin
     * reloads, and has the [ClaudeSessionKeeper] save their sessions first, for the reloaded plugin to take them back by,
     * and stop, so that it doesn't save the view as it empties. A tab whose Claude Code has exited goes with the tool
     * window, and the reloaded plugin resumes its session, as it would after a restart.
     */
    fun parkTheTabs() {
        ClaudeSessionKeeper.getInstance(project).saveOneLastTimeAndStop()
        TabsParkedInTheTerminal(project).park(tabs.filter { it.claudeCode != null })
    }

    /** Opens a tab that starts a new session in the project's folder, and shows it. */
    fun startNewSession(): ClaudeSessionTab =
        openTab(ClaudeCodeCommand.startingANewSession(), Path.of(project.basePath!!), resuming = null).also(::show)

    /** Opens a tab that resumes [session] in its folder. */
    fun resume(session: ClaudeSession): ClaudeSessionTab =
        openTab(ClaudeCodeCommand.resuming(session), session.workingDirectory, resuming = session)

    /** Shows [tab] in the tool window, and the tool window. */
    fun show(tab: ClaudeSessionTab) {
        val toolWindow = toolWindow!!
        toolWindow.contentManager.setSelectedContent(tab.content, true)
        toolWindow.activate(null)
    }

    /**
     * Adds [terminal], which runs [session], as a tab. It adds the terminal's content itself, as
     * [TabsParkedInTheTerminal] does, and says why.
     */
    private fun adopt(terminal: TerminalToolWindowTab, session: ClaudeSession): ClaudeSessionTab {
        val tab = ClaudeSessionTab(terminal, session)
        tabsByContent[tab.content] = tab
        toolWindow!!.contentManager.addContent(terminal.content)
        return tab
    }

    /**
     * Opens a tab whose terminal runs the user's shell in [workingDirectory], and types [command] into it. The terminal
     * plugin neither restores the tab nor saves it: the [ClaudeSessionKeeper] does.
     */
    private fun openTab(command: String, workingDirectory: Path, resuming: ClaudeSession?): ClaudeSessionTab {
        val terminal = TerminalToolWindowTabsManager.getInstance(project).createTabBuilder()
            .workingDirectory(workingDirectory.toString())
            .tabName(resuming?.name ?: "Claude")
            .contentManager(toolWindow!!.contentManager)
            .closeOnProcessTermination(true)
            .restoreOnProjectReopen(false)
            .deferSessionStartUntilUiShown(false)
            .requestFocus(false)
            .createTab()
        terminal.view.createSendTextBuilder().shouldExecute().send(command)
        val tab = ClaudeSessionTab(terminal, resuming)
        tabsByContent[tab.content] = tab
        return tab
    }

    companion object {
        /** The tool window's id, which plugin.xml registers it with. */
        const val TOOL_WINDOW_ID = "Claude Sessions"

        fun getInstance(project: Project): ClaudeSessionsView = project.service()

        /** The project's Claude Sessions tool window, which plugin.xml registers. */
        fun toolWindowOf(project: Project): ToolWindow? = ToolWindowManager.getInstance(project).getToolWindow(TOOL_WINDOW_ID)
    }
}
