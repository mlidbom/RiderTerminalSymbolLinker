package dev.magnus.claudesessions

import com.intellij.openapi.Disposable
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowManager
import com.intellij.openapi.wm.WindowManager
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTab
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTabsManager
import com.intellij.ui.content.Content
import com.intellij.ui.content.ContentManagerEvent
import com.intellij.ui.content.ContentManagerListener
import dev.magnus.claudesessions.claudecode.ClaudeCodeCommand
import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.reload.TabsParkedInTheTerminal
import dev.magnus.claudesessions.saving.ClaudeSessionKeeper
import dev.magnus.claudesessions.saving.SavedClaudeSessions
import java.nio.file.Path
import java.util.concurrent.ConcurrentHashMap

/**
 * A project's Claude Sessions tool window: a tab for each Claude Code session, each a [ClaudeSessionTab] whose shell
 * runs Claude Code. When the tool window opens, it resumes the sessions the [ClaudeSessionKeeper] saved, so that they
 * survive the IDE's restart.
 *
 * Its methods run on the EDT.
 */
@Service(Service.Level.PROJECT)
class ClaudeSessionsView(val project: Project) : Disposable {
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
                tabsByContent.remove(event.content)?.takeAwayItsChat()
            }
        })
        val saved = SavedClaudeSessions(project).read()
        val parked = TabsParkedInTheTerminal(project).takeBack(saved.sessions)
        val takenBack = parked.map { adopt(it.terminal, it.session) }
        takenBack.forEach { it.giveItAChat(project, this) }
        val sessionsTakenBack = parked.mapNotNull { it.session?.id }.toSet()
        val tabs = takenBack + saved.sessions.filter { it.id !in sessionsTakenBack && it.canBeResumed }.map(::resume)
        tabs.firstOrNull { it.session?.id == saved.selected }?.let { toolWindow.contentManager.setSelectedContent(it.content, false) }
        ClaudeSessionKeeper.getInstance(project).startKeeping()
    }

    /**
     * Moves the tabs to the Terminal tool window, where their sessions run on while the plugin reloads, and has the
     * [ClaudeSessionKeeper] save their sessions first, and the selected tab, and stop, so that it doesn't save the view
     * as it empties. Each tab's chat is taken away first, giving its content its terminal back: the reloaded plugin gives
     * it a chat of its own.
     */
    fun parkTheTabs() {
        ClaudeSessionKeeper.getInstance(project).saveOneLastTimeAndStop()
        val tabs = tabs
        tabs.forEach(ClaudeSessionTab::takeAwayItsChat)
        TabsParkedInTheTerminal(project).park(tabs)
    }

    /** The project's folder, where a new session starts. */
    private val projectFolder: Path get() = Path.of(project.basePath!!)

    /** Opens a tab that starts a new session in the project's folder, named [name] unless that's null, and shows it. */
    fun startNewSession(name: String?): ClaudeSessionTab =
        openTab(ClaudeCodeCommand.startingANewSession(name), projectFolder, resuming = null, tabName = name, shownAtOnce = true)

    /**
     * Opens a tab that resumes [session] in its folder, as after a restart, without showing it.
     *
     * bug: the tab [open] selects as the IDE starts is shown at once when the tool window is, but its shell starts at
     *   once all the same, so it can keep 80 columns by 20 rows, as [openTab] says: seen in the sandbox on 2026-11-19.
     *   Its shell can't simply wait until the tab is shown, since its session must run even if the tool window never is.
     */
    fun resume(session: ClaudeSession): ClaudeSessionTab =
        openTab(ClaudeCodeCommand.resuming(session), session.workingDirectory, resuming = session, tabName = session.name, shownAtOnce = false)

    /**
     * Opens a tab that resumes [session] in the project's folder, where a new session starts, whichever folder the session
     * ran in before, and shows it: as the session history picker does. Claude Code finds the session's transcript in
     * any project's folder, and goes on with it in the folder it runs in.
     */
    fun resumeInTheProjectsFolder(session: ClaudeSession): ClaudeSessionTab =
        openTab(ClaudeCodeCommand.resuming(session), projectFolder, resuming = session, tabName = session.name, shownAtOnce = true)

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
     * Opens a tab named [tabName], or else "Claude", whose terminal runs the user's shell in [workingDirectory], has the
     * shell run [command], and shows the tab if it's [shownAtOnce]. The terminal plugin neither restores the tab nor
     * saves it: the [ClaudeSessionKeeper] does.
     *
     * A tab shown at once starts its shell once the tab is laid out, at the tab's size, so Claude Code starts at it too.
     * One that isn't, as those resumed as the IDE starts, starts its shell at once, so that its session runs before the
     * tab is ever shown: at 80 columns by 20 rows, the terminal's guess, until the tab is first shown.
     *
     * A tab shown at once whose shell started at once, as every new tab's did until 2026-11-19, often kept 80 by 20,
     * and Claude Code drawn at that size, until the user resized it: the terminal drops a resize that comes before its
     * shell has started, and sends none once it has.
     */
    private fun openTab(command: ClaudeCodeCommand, workingDirectory: Path, resuming: ClaudeSession?, tabName: String?, shownAtOnce: Boolean): ClaudeSessionTab {
        val terminal = TerminalToolWindowTabsManager.getInstance(project).createTabBuilder()
            .workingDirectory(workingDirectory.toString())
            .tabName(tabName ?: "Claude")
            .contentManager(toolWindow!!.contentManager)
            .closeOnProcessTermination(true)
            .restoreOnProjectReopen(false)
            .deferSessionStartUntilUiShown(shownAtOnce)
            .requestFocus(false)
            .createTab()
        val tab = ClaudeSessionTab(terminal, resuming)
        tabsByContent[tab.content] = tab
        tab.giveItAChat(project, this)
        tab.run(command)
        if (shownAtOnce) show(tab)
        return tab
    }

    /** Gives up the tabs' chats, as the project closes or the plugin unloads: their editors are disposed with them. */
    override fun dispose() {}

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
