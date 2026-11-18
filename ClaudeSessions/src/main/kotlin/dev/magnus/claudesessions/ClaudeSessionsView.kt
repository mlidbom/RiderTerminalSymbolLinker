package dev.magnus.claudesessions

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowManager
import com.intellij.openapi.wm.WindowManager
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

    /**
     * The tabs, in the order the tool window shows them: every terminal tab in it. Those the view didn't open itself are
     * its tabs as much, however they came in: such a tab ran resident-server, a session of Magnus's, and the view, which
     * knew only the tabs it opened, neither saved it nor kept it through a reload (2026-11-17). How that tab came into the
     * tool window wasn't found: the terminal's own New Tab action is disabled outside the Terminal tool window.
     */
    val tabs: List<ClaudeSessionTab>
        get() = toolWindow?.contentManager?.contents?.mapNotNull(::tabOf).orEmpty()

    /** The tab the tool window shows. */
    val selectedTab: ClaudeSessionTab?
        get() = toolWindow?.contentManager?.selectedContent?.let(::tabOf)

    /** The tab whose content [content] is, if it's a terminal's: one the view opened, or else one it takes on now. */
    private fun tabOf(content: Content): ClaudeSessionTab? =
        tabsByContent[content] ?: content.getUserData(TerminalToolWindowTab.KEY)?.let { terminal -> tabsByContent.getOrPut(content) { ClaudeSessionTab(terminal, resuming = null) } }

    /**
     * Takes [toolWindow] as the view, as [ClaudeSessionsToolWindowFactory] gives it its content, and has the
     * [ClaudeSessionKeeper] start keeping its sessions. The tabs [TabsParkedInTheTerminal] while the plugin reloaded come
     * back as they were, in their order, and then the saved sessions none of them ran, resumed, unless they can't be:
     * archived or deleted since they ran. After a restart, that's all of them. The tab that was selected is selected
     * again.
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
        val takenBack = parked.map { adopt(it.terminal, it.session) }
        val sessionsTakenBack = parked.mapNotNull { it.session?.id }.toSet()
        val tabs = takenBack + saved.sessions.filter { it.id !in sessionsTakenBack && it.canBeResumed }.map(::resume)
        tabs.firstOrNull { it.session?.id == saved.selected }?.let { toolWindow.contentManager.setSelectedContent(it.content, false) }
        ClaudeSessionKeeper.getInstance(project).startKeeping()
    }

    /**
     * Moves the tabs to the Terminal tool window, where their sessions run on while the plugin reloads, and has the
     * [ClaudeSessionKeeper] save their sessions first, and the selected tab, and stop, so that it doesn't save the view
     * as it empties.
     */
    fun parkTheTabs() {
        ClaudeSessionKeeper.getInstance(project).saveOneLastTimeAndStop()
        TabsParkedInTheTerminal(project).park(tabs)
    }

    /** The project's folder, where a new session starts. */
    private val projectFolder: Path get() = Path.of(project.basePath!!)

    /** Opens a tab that starts a new session in the project's folder, and shows it. */
    fun startNewSession(): ClaudeSessionTab =
        openTab(ClaudeCodeCommand.startingANewSession(), projectFolder, resuming = null).also(::show)

    /** Opens a tab that resumes [session] in its folder, as after a restart. */
    fun resume(session: ClaudeSession): ClaudeSessionTab =
        openTab(ClaudeCodeCommand.resuming(session), session.workingDirectory, resuming = session)

    /**
     * Opens a tab that resumes [session] in the project's folder, where a new session starts, whichever folder the session
     * ran in before, and shows it: as the session history picker does. Claude Code finds the session's transcript in
     * any project's folder, and goes on with it in the folder it runs in.
     */
    fun resumeInTheProjectsFolder(session: ClaudeSession): ClaudeSessionTab =
        openTab(ClaudeCodeCommand.resuming(session), projectFolder, resuming = session).also(::show)

    /** Resumes [session] in [tab], whose Claude Code has exited, and brings the tab forward. */
    fun resumeIn(tab: ClaudeSessionTab, session: ClaudeSession) {
        tab.resume(session)
        bringForward(tab)
    }

    /**
     * Brings [tab] forward, as for a notification about its session that the user clicked, or for the session history
     * picker of another IDE: the tab shown, as [show] does, and the project's window in front of the IDE's others.
     * Windows lets an application that isn't active bring its window forward only when the active one allowed it, as
     * the other IDE's picker does, and as the tool the user clicked does by bringing the IDE forward itself. Otherwise
     * the window's taskbar button flashes.
     */
    fun bringForward(tab: ClaudeSessionTab) {
        WindowManager.getInstance().getFrame(project)?.toFront()
        show(tab)
    }

    /** Shows [tab] in the tool window, and the tool window. */
    fun show(tab: ClaudeSessionTab) {
        val toolWindow = toolWindow!!
        toolWindow.contentManager.setSelectedContent(tab.content, true)
        toolWindow.activate(null)
    }

    /**
     * Adds [terminal], which ran [session] last, if it ran one, as a tab. It adds the terminal's content itself, as
     * [TabsParkedInTheTerminal] does, and says why.
     */
    private fun adopt(terminal: TerminalToolWindowTab, session: ClaudeSession?): ClaudeSessionTab {
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

        /**
         * The project's view, opened: the IDE gives the tool window its content as it's first asked for it, which it is
         * otherwise only as it first shows the tool window.
         */
        fun openIn(project: Project): ClaudeSessionsView {
            toolWindowOf(project)!!.contentManager
            return getInstance(project)
        }

        /** The project's Claude Sessions tool window, which plugin.xml registers. */
        fun toolWindowOf(project: Project): ToolWindow? = ToolWindowManager.getInstance(project).getToolWindow(TOOL_WINDOW_ID)
    }
}
