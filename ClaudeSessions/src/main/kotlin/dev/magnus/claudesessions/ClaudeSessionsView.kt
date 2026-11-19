package dev.magnus.claudesessions

import com.intellij.notification.NotificationType
import com.intellij.openapi.Disposable
import com.intellij.openapi.application.EDT
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.diagnostic.thisLogger
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowManager
import com.intellij.openapi.wm.WindowManager
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTab
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTabsManager
import com.intellij.ui.content.Content
import com.intellij.ui.content.ContentManagerEvent
import com.intellij.ui.content.ContentManagerListener
import dev.magnus.claudesessions.chat.saving.SavedChats
import dev.magnus.claudesessions.claudecode.ClaudeCodeCommand
import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.hub.ClaudeToolingHub
import dev.magnus.claudesessions.hub.SessionHostsAnswer
import dev.magnus.claudesessions.saving.ClaudeSessionKeeper
import dev.magnus.claudesessions.saving.SavedClaudeSessions
import dev.magnus.claudesessions.saving.SavedTab
import dev.magnus.claudesessions.sessionhosts.SessionHost
import dev.magnus.claudesessions.sessionhosts.SessionHostId
import dev.magnus.claudesessions.sessionhosts.SessionHostLabel
import dev.magnus.claudesessions.sessionhosts.TheTerminalsShell
import java.nio.file.Path
import java.util.concurrent.ConcurrentHashMap
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Deferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * A project's Claude Sessions tool window: a tab for each Claude Code session, each a [ClaudeSessionTab] whose shell
 * runs Claude Code in a [SessionHost] outside the IDE. When the tool window opens, the tabs the [ClaudeSessionKeeper]
 * saved come back: attached to their hosts again, whose sessions ran on while the IDE was gone, or, when a host is gone
 * too, as after a reboot, with the session resumed in a new one.
 *
 * Closing a tab ends its session. The IDE exiting, the project closing, or this plugin unloading only detaches the tabs
 * from their hosts: see [detachTheTabs].
 *
 * Its methods run on the EDT.
 */
@Service(Service.Level.PROJECT)
class ClaudeSessionsView(val project: Project, private val scope: CoroutineScope) : Disposable {
    private var toolWindow: ToolWindow? = null
    private val tabsByContent = ConcurrentHashMap<Content, ClaudeSessionTab>()

    /** Whether the tabs are going with the view, and not closed by the user: their sessions then run on in their hosts. */
    private var detaching = false

    /** The tabs' chats, as their keepers saved them. */
    private val savedChats = SavedChats(project)

    private val hub: ClaudeToolingHub get() = ClaudeToolingHub.getInstance()

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

    /** The tab whose content [content] is, if it's a terminal's: one the view opened, or else one it takes on now, without a session host. */
    private fun tabOf(content: Content): ClaudeSessionTab? =
        tabsByContent[content] ?: content.getUserData(TerminalToolWindowTab.KEY)?.let { terminal -> tabsByContent.getOrPut(content) { ClaudeSessionTab(project, terminal, resuming = null, sessionHost = null) } }

    /**
     * Takes [toolWindow] as the view, as [ClaudeSessionsToolWindowFactory] gives it its content, and brings back the tabs
     * the [ClaudeSessionKeeper] saved, as [bringBackTheSavedTabs] says.
     */
    fun open(toolWindow: ToolWindow) {
        this.toolWindow = toolWindow
        toolWindow.contentManager.addContentManagerListener(object : ContentManagerListener {
            override fun contentRemoved(event: ContentManagerEvent) {
                tabsByContent.remove(event.content)?.let(::closed)
            }
        })
        scope.launch(Dispatchers.EDT) { bringBackTheSavedTabs() }
    }

    /**
     * Brings back the saved tabs, in their order: each whose [SessionHost] still runs attached to it again, as it is, its
     * Claude Code included, and each whose host is gone resumed in a new one, unless its session can't be resumed:
     * archived or deleted since it ran. Then the hosts this view started and didn't save, as when the IDE crashed before
     * its keeper looked, unless another attach client shows them. The tab that was selected is selected again, and the
     * [ClaudeSessionKeeper] starts keeping the tabs.
     *
     * When the hub can't tell which hosts run, the saved tabs stay saved for the next start, and the keeper doesn't start:
     * it would save the view without them.
     */
    private suspend fun bringBackTheSavedTabs() {
        val saved = SavedClaudeSessions(project).read()
        val running = when (val answer = withContext(Dispatchers.IO) { hub.sessionHosts.running() }) {
            is SessionHostsAnswer.Done -> answer.result
            is SessionHostsAnswer.NotDone -> {
                if (saved.tabs.isEmpty()) return ClaudeSessionKeeper.getInstance(project).startKeeping()
                return tell("The Claude sessions of ${project.name} didn't come back", "${answer.why} They come back when the IDE next starts.")
            }
        }
        val tabs = saved.tabs.mapNotNull { bringBack(it, running) }.toMutableList()
        val claimed = tabs.mapNotNull { it.sessionHost?.id }.toSet()
        val label = SessionHostLabel.ofTheViewOf(project)
        running.filter { it.id !in claimed && !it.attached && SessionHostLabel.of(it.label) == label }.forEach { tabs += openTab(it, resuming = null, tabName = null, projectFolder) }
        savedChats.deleteAllBut(tabs.mapNotNull { it.sessionHost?.id }.toSet())
        thisLogger().info("Brought back ${tabs.size} tabs of ${project.name}: ${tabs.joinToString { "'${it.content.displayName}' (session host ${it.sessionHost?.id?.value}, session ${it.session?.id?.value})" }}")
        tabs.firstOrNull { it.session?.id == saved.selected }?.let { toolWindow!!.contentManager.setSelectedContent(it.content, false) }
        ClaudeSessionKeeper.getInstance(project).startKeeping()
    }

    /**
     * Brings back the tab [saved] says, if it can come back, from among the [running] session hosts, with its chat as it
     * was saved: by its host, the one it attaches to again, or the one its session ran in, before it's resumed in a new
     * one.
     */
    private suspend fun bringBack(saved: SavedTab, running: List<SessionHost>): ClaudeSessionTab? {
        val host = running.firstOrNull { it.id == saved.sessionHost }
        val session = saved.session
        return when {
            host != null -> openTab(host, session, session?.name, session?.workingDirectory ?: projectFolder)
            session != null && withContext(Dispatchers.IO) { session.canBeResumed } -> openTabInANewHost(ClaudeCodeCommand.Resuming(session), session.workingDirectory, session, session.name, chatSavedAs = saved.sessionHost)
            else -> null
        }
    }

    /**
     * Detaches the tabs from their session hosts as the view goes, though the user didn't close them: the IDE exits, the
     * project closes, or this plugin unloads, as for an update. Their sessions run on, and the view attaches to them again
     * when it next opens. The [ClaudeSessionKeeper] saves the tabs one last time first, and stops, so that it doesn't save
     * the view as it empties. Each tab's chat is saved, and taken away while the tool window is still there, giving its
     * content its terminal back: disposed with this view, after the tool window, a chat gave its terminal to a content
     * whose tool window had no UI any more, and the IDE reported the failure.
     */
    fun detachTheTabs() {
        ClaudeSessionKeeper.getInstance(project).saveOneLastTimeAndStop()
        detaching = true
        tabs.forEach(ClaudeSessionTab::detachItsChat)
    }

    /**
     * [tab] has closed, unless the view is going: its chat goes, forgotten, and its session ends with it, when the user
     * closed it, and its attach client didn't end by itself.
     */
    private fun closed(tab: ClaudeSessionTab) {
        if (detaching) return
        tab.closeItsChat()
        val host = tab.sessionHost ?: return
        if (tab.closedAsItsAttachClientEnded) return
        scope.launch {
            val answer = hub.sessionHosts.end(host.id)
            if (answer is SessionHostsAnswer.NotDone) tell("The session of the tab ${tab.content.displayName} may run on", answer.why)
        }
    }

    /** The project's folder, where a new session starts. */
    private val projectFolder: Path get() = Path.of(project.basePath!!)

    /**
     * Opens a tab that starts a new session in the project's folder, named [name] unless that's null, and shows it.
     * Returns the tab, once its session host has started, or null when it couldn't, which the user is told.
     */
    fun startNewSession(name: String?): Deferred<ClaudeSessionTab?> = scope.async(Dispatchers.EDT) {
        openTabInANewHost(ClaudeCodeCommand.StartingANewSession(name), projectFolder, resuming = null, tabName = name)?.also(::show)
    }

    /**
     * Opens a tab that resumes [session] in the project's folder, where a new session starts, whichever folder the session
     * ran in before, and shows it: as the session history picker does. Claude Code finds the session's transcript in
     * any project's folder, and goes on with it in the folder it runs in.
     */
    fun resumeInTheProjectsFolder(session: ClaudeSession): Deferred<ClaudeSessionTab?> = scope.async(Dispatchers.EDT) {
        openTabInANewHost(ClaudeCodeCommand.Resuming(session), projectFolder, resuming = session, tabName = session.name)?.also(::show)
    }

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

    /** Closes [tab], as its close button does: its session ends. */
    fun close(tab: ClaudeSessionTab) {
        toolWindow!!.contentManager.removeContent(tab.content, true)
    }

    /**
     * Starts a session host for a tab whose session runs in [workingDirectory], and opens the tab, named [tabName], or else
     * "Claude", on it, and has its shell run [command]. The tab's chat comes back as it was saved as the chat of the
     * session host [chatSavedAs], if it was. Null when the host didn't start, which the user is told.
     */
    private suspend fun openTabInANewHost(command: ClaudeCodeCommand, workingDirectory: Path, resuming: ClaudeSession?, tabName: String?, chatSavedAs: SessionHostId? = null): ClaudeSessionTab? {
        //bug: a host the hub starts as the view goes is left without a tab: the view's scope is cancelled while the hub
        //   starts it, so no tab opens on it, and the next view takes it on as a tab of its own, named "Claude", whose shell
        //   runs no Claude Code, beside the tab that resumes the session again in another host. Seen in the sandbox on
        //   2026-11-19, as the IDE reloaded the plugin twice within a second, having seen its jar change twice, while the
        //   view brought back a plain tab's session; the same comes of a reload while the view resumes sessions after a
        //   reboot. A fix: end the host when the view went while it started.
        val host = startASessionHost(workingDirectory) ?: return null
        return openTab(host, resuming, tabName, workingDirectory, chatSavedAs ?: host.id).also { it.runInItsNewShell(command) }
    }

    /**
     * Has the hub start a session host whose console runs the shell the IDE's terminal runs, in [workingDirectory], with
     * the environment the terminal gives it, labelled as this view's. Null when it didn't, which the user is told.
     */
    private suspend fun startASessionHost(workingDirectory: Path): SessionHost? {
        val shell = TheTerminalsShell(project)
        val label = SessionHostLabel.ofTheViewOf(project)
        return when (val answer = withContext(Dispatchers.IO) { hub.sessionHosts.start(shell.commandLine, workingDirectory, shell.environment(), label) }) {
            is SessionHostsAnswer.Done -> answer.result
            is SessionHostsAnswer.NotDone -> null.also { tell("A Claude session didn't start", answer.why) }
        }
    }

    /**
     * Opens a tab named [tabName], or else "Claude", whose terminal runs [host]'s attach client, which shows the session
     * in it, [resuming] or another, as it is. The terminal plugin neither restores the tab nor saves it: the
     * [ClaudeSessionKeeper] does.
     *
     * The terminal starts the attach client only once the tab is first shown, at the tab's size: the host's console takes
     * it, and Claude Code in it draws itself again at it. Until then the session runs in the host at the host's own size.
     * Until 2026-11-19, when a tab's terminal ran its shell itself, a tab whose shell started before it was laid out could
     * keep the terminal's guess, 80 columns by 20 rows: the terminal drops a resize that comes before its process has
     * started.
     *
     * Its chat comes back as it was saved as the chat of the session host [chatSavedAs], if it was: [host], or the one
     * the session ran in before it was resumed in [host].
     */
    private fun openTab(host: SessionHost, resuming: ClaudeSession?, tabName: String?, workingDirectory: Path, chatSavedAs: SessionHostId = host.id): ClaudeSessionTab {
        val toolWindow = toolWindow!!
        val terminal = TerminalToolWindowTabsManager.getInstance(project).createTabBuilder()
            .workingDirectory(workingDirectory.toString())
            .shellCommand(host.attachCommand)
            .tabName(tabName ?: "Claude")
            .contentManager(toolWindow.contentManager)
            .closeOnProcessTermination(false)
            .restoreOnProjectReopen(false)
            .deferSessionStartUntilUiShown(true)
            .requestFocus(false)
            .createTab()
        val tab = ClaudeSessionTab(project, terminal, resuming, host)
        tabsByContent[tab.content] = tab
        tab.giveItAChat(this, savedChats, savedChats.read(chatSavedAs))
        tab.closeAsItsAttachClientEnds { toolWindow.contentManager.removeContent(tab.content, true) }
        return tab
    }

    private fun tell(title: String, why: String) {
        thisLogger().warn("$title: $why")
        ClaudeSessionsNotifications.tell(project, title, why, NotificationType.WARNING)
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
