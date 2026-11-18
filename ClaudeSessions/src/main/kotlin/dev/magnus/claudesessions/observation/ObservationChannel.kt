package dev.magnus.claudesessions.observation

import com.google.gson.Gson
import com.google.gson.GsonBuilder
import com.intellij.openapi.Disposable
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.intellij.openapi.project.ProjectManager
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.openapi.vfs.VfsUtil
import com.intellij.openapi.wm.ToolWindowManager
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTabsManager
import com.sun.net.httpserver.HttpExchange
import com.sun.net.httpserver.HttpServer
import dev.magnus.claudesessions.ClaudeSession
import dev.magnus.claudesessions.ClaudeSessionKeeper
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.SavedClaudeSessions
import dev.magnus.claudesessions.history.ClaudeSessionHistoryPicker
import dev.magnus.claudesessions.history.ClaudeSessionHistoryScope
import dev.magnus.claudesessions.history.WhereTheSessionIs
import com.intellij.openapi.keymap.KeymapManager
import javax.swing.KeyStroke
import java.awt.event.KeyEvent
import org.jetbrains.plugins.terminal.TerminalToolWindowFactory
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.URLDecoder
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors

/**
 * A development-only HTTP server through which the development tools see and drive what the plugin does in the
 * sandbox IDE. It listens only when the system property [PORT_PROPERTY] names a port, which only the sandbox's
 * `idea.properties` sets, so a user's IDE never has it.
 *
 * Every endpoint answers JSON. `project` names the open project to ask about, and may be left out when one is open:
 *  - `GET /state` — each open project's [ClaudeSessionsView]: its tabs, each with the process its terminal runs, its
 *    shell, the Claude Code that runs below it and that one's parent process, and its session, the
 *    [SavedClaudeSessions], and whether the [ClaudeSessionKeeper] keeps them.
 *  - `POST /view/open?project=` — has the IDE give the view its content, as showing the tool window would.
 *  - `POST /sessions/new?project=` — starts a new session in a tab, as the New Claude Session action does.
 *  - `POST /tabs/new-plain?project=` — opens a plain terminal tab in the view's tool window, not through the view, as
 *    a tab can come into it that the view didn't open, and answers with its place among the view's tabs.
 *  - `POST /tabs/select?project=&tab=` — selects the tab, and shows the tool window, as a click on the tab does.
 *  - `GET /tabs/screen?project=&tab=` — the text the tab's terminal shows, from the top of its screen.
 *  - `POST /tabs/type?project=&tab=` with `{"text": …}` — types the text into the tab's terminal, and Enter.
 *  - `POST /tabs/close?project=&tab=` — closes the tab by ending its processes, as its shell's exit does: closing it
 *    as its close button does asks the user to confirm, and that dialog would wait for a human.
 *  - `POST /tabs/close-all?project=` — closes every tab so, and answers once they're gone, so that a specification
 *    starts with none.
 *  - `POST /history/open?project=` — opens the session history picker, as the Resume a Claude Session action does,
 *    and answers as `GET /history` once it shows.
 *  - `GET /history?project=` — the picker shown in the project's window: its scope, the scopes it offers, its filter,
 *    its entries, each with where its session is and whether it can be chosen, and the one selected.
 *  - `POST /history/filter?project=` with `{"text": …}` — types the filter into the picker's search field.
 *  - `POST /history/scope?project=&scope=` — lists the scope, by its name, as a click on its button does.
 *  - `POST /history/choose?project=&entry=` — selects the entry, by its place, and chooses it, as Enter does.
 *  - `POST /history/close?project=` — closes the picker, as Escape does.
 *  - `POST /history/press?project=&key=` — has the picker's search field take the key, `UP`, `DOWN` or `ENTER`, as
 *    from the keyboard.
 *  - `POST /history/double-click?project=&entry=` — double-clicks the entry, by its place.
 *  - `GET /keymap?keystroke=&keymap=` — the actions the keymap named, or else the active one, binds the keystroke to,
 *    as `KeyStroke.getKeyStroke` reads it, such as `ctrl alt shift H`: to find a shortcut that is free.
 *  - `POST /plugins/refresh` — has the IDE look at its plugins folder again, so that with `idea.auto.reload.plugins` it
 *    reloads a plugin whose files changed, this one included. `/state`'s `startedAt` then tells when the new build's
 *    channel started.
 */
@Service(Service.Level.APP)
class ObservationChannel : Disposable {
    private val gson: Gson = GsonBuilder().disableHtmlEscaping().serializeNulls().create()

    /** When this channel started, in milliseconds since the epoch: with each build of the plugin loaded, a new one does. */
    private val startedAt = System.currentTimeMillis()

    private val server: HttpServer? = System.getProperty(PORT_PROPERTY)?.toInt()?.let(::listenOn)

    private fun listenOn(port: Int): HttpServer =
        HttpServer.create(InetSocketAddress(InetAddress.getLoopbackAddress(), port), 0).apply {
            // Each request on a thread of its own: on the server's one thread, a request waiting for the EDT deadlocks a
            // reload of the plugin, whose unloading stops this server on the EDT and waits for that thread.
            executor = Executors.newCachedThreadPool()
            createContext("/state") { exchange -> answer(exchange) { state() } }
            createContext("/view/open") { exchange -> answer(exchange) { openTheView(projectOf(exchange)) } }
            createContext("/sessions/new") { exchange -> answer(exchange) { startANewSession(projectOf(exchange)) } }
            createContext("/tabs/type") { exchange ->
                answer(exchange) { type(projectOf(exchange), query(exchange, "tab")!!.toInt(), request<TextRequest>(exchange).text) }
            }
            createContext("/tabs/new-plain") { exchange -> answer(exchange) { openAPlainTerminalTab(projectOf(exchange)) } }
            createContext("/tabs/select") { exchange -> answer(exchange) { select(projectOf(exchange), query(exchange, "tab")!!.toInt()) } }
            createContext("/tabs/screen") { exchange -> answer(exchange) { screen(projectOf(exchange), query(exchange, "tab")!!.toInt()) } }
            createContext("/tabs/close-all") { exchange -> answer(exchange) { closeEveryTab(projectOf(exchange)) } }
            createContext("/tabs/close") { exchange -> answer(exchange) { close(projectOf(exchange), query(exchange, "tab")!!.toInt()) } }
            createContext("/plugins/refresh") { exchange -> answer(exchange) { refreshThePluginsFolder() } }
            createContext("/history/open") { exchange -> answer(exchange) { openThePicker(projectOf(exchange)) } }
            createContext("/history/filter") { exchange ->
                answer(exchange) { inThePicker(projectOf(exchange)) { it.filter = request<TextRequest>(exchange).text } }
            }
            createContext("/history/scope") { exchange ->
                answer(exchange) { inThePicker(projectOf(exchange)) { it.list(ClaudeSessionHistoryScope.valueOf(query(exchange, "scope")!!)) } }
            }
            createContext("/history/choose") { exchange ->
                answer(exchange) {
                    inThePicker(projectOf(exchange)) {
                        it.selected = query(exchange, "entry")!!.toInt()
                        it.chooseTheSelected()
                    }
                }
            }
            createContext("/history/close") { exchange -> answer(exchange) { inThePicker(projectOf(exchange)) { it.close() } } }
            createContext("/history/press") { exchange ->
                answer(exchange) { inThePicker(projectOf(exchange)) { it.press(KEYS.getValue(query(exchange, "key")!!)) } }
            }
            createContext("/history/double-click") { exchange ->
                answer(exchange) { inThePicker(projectOf(exchange)) { it.doubleClick(query(exchange, "entry")!!.toInt()) } }
            }
            createContext("/history") { exchange -> answer(exchange) { onTheEdt { describeThePickerIn(projectOf(exchange)) } } }
            createContext("/keymap") { exchange -> answer(exchange) { actionsBoundTo(query(exchange, "keystroke")!!, query(exchange, "keymap")) } }
            start()
        }

    override fun dispose() {
        server?.stop(0)
        (server?.executor as ExecutorService?)?.shutdown()
    }

    private fun state(): Map<String, Any?> = mapOf(
        "startedAt" to startedAt,
        "projects" to ProjectManager.getInstance().openProjects.map { project ->
            val saved = SavedClaudeSessions(project)
            mapOf(
                "name" to project.name,
                "basePath" to project.basePath,
                "view" to describe(ClaudeSessionsView.getInstance(project)),
                "keeping" to ClaudeSessionKeeper.getInstance(project).isKeeping,
                "terminalToolWindow" to describeTheTerminalToolWindow(project),
                "saved" to mapOf("file" to saved.location.toString(), "sessions" to saved.read().sessions.map(::describe), "selected" to saved.read().selected?.value),
            )
        },
    )

    /** The Terminal tool window, where the view's tabs are parked while the plugin reloads. */
    private fun describeTheTerminalToolWindow(project: Project): Map<String, Any?> {
        val toolWindow = ToolWindowManager.getInstance(project).getToolWindow(TerminalToolWindowFactory.TOOL_WINDOW_ID)
        val tabs = TerminalToolWindowTabsManager.getInstance(project).tabs
        return mapOf("visible" to toolWindow?.isVisible, "tabs" to tabs.map { mapOf("name" to it.content.displayName, "processId" to ClaudeSessionTab.processOf(it)?.pid()) })
    }

    private fun describe(view: ClaudeSessionsView): Map<String, Any?> = mapOf(
        "open" to view.isOpen,
        "toolWindowVisible" to ClaudeSessionsView.toolWindowOf(view.project)?.isVisible,
        "tabs" to view.tabs.map { tab -> describe(tab) },
        "selectedTab" to view.selectedTab?.let(view.tabs::indexOf),
    )

    private fun describe(tab: ClaudeSessionTab): Map<String, Any?> = mapOf(
        "name" to tab.content.displayName,
        "processId" to tab.process?.pid(),
        "claudeCode" to tab.claudeCode?.process?.let { mapOf("processId" to it.pid(), "parentProcessId" to it.parent().orElse(null)?.pid()) },
        "session" to tab.lookAtTheSession()?.let(::describe),
    )

    private fun describe(session: ClaudeSession): Map<String, Any?> =
        mapOf("id" to session.id.value, "workingDirectory" to session.workingDirectory.toString(), "name" to session.name)

    private fun openTheView(project: Project): Map<String, Any?> = onTheEdt {
        ClaudeSessionsView.toolWindowOf(project)!!.contentManager
        describe(ClaudeSessionsView.getInstance(project))
    }

    private fun startANewSession(project: Project): Map<String, Any?> = onTheEdt {
        ClaudeSessionsView.toolWindowOf(project)!!.contentManager
        val view = ClaudeSessionsView.getInstance(project)
        val tab = view.startNewSession()
        mapOf("tab" to view.tabs.indexOf(tab))
    }

    private fun openAPlainTerminalTab(project: Project): Map<String, Any?> = onTheEdt {
        val toolWindow = ClaudeSessionsView.toolWindowOf(project)!!
        val terminal = TerminalToolWindowTabsManager.getInstance(project).createTabBuilder()
            .contentManager(toolWindow.contentManager)
            .requestFocus(false)
            .createTab()
        mapOf("tab" to ClaudeSessionsView.getInstance(project).tabs.indexOfFirst { it.terminal == terminal })
    }

    private fun select(project: Project, tab: Int): Map<String, Any?> = onTheEdt {
        val view = ClaudeSessionsView.getInstance(project)
        view.show(view.tabs[tab])
        mapOf("selected" to tab)
    }

    private fun type(project: Project, tab: Int, text: String): Map<String, Any?> = onTheEdt {
        ClaudeSessionsView.getInstance(project).tabs[tab].terminal.view.createSendTextBuilder().shouldExecute().send(text)
        mapOf("typed" to text)
    }

    private fun screen(project: Project, tab: Int): Map<String, Any?> = onTheEdt {
        val output = ClaudeSessionsView.getInstance(project).tabs[tab].terminal.view.outputModels.active.value
        mapOf("lines" to output.getText(output.screenTopOffset, output.endOffset).toString().lines())
    }

    private fun close(project: Project, tab: Int): Map<String, Any?> {
        end(ClaudeSessionsView.getInstance(project).tabs[tab])
        return mapOf("closed" to tab)
    }

    private fun closeEveryTab(project: Project): Map<String, Any?> {
        val view = ClaudeSessionsView.getInstance(project)
        val tabs = view.tabs
        tabs.forEach(::end)
        val deadline = System.currentTimeMillis() + 10_000
        while (view.tabs.isNotEmpty() && System.currentTimeMillis() < deadline) Thread.sleep(100)
        return mapOf("closed" to tabs.size, "left" to view.tabs.size)
    }

    /** Ends the process [tab] runs, and those below it, so that the tab closes as when Claude Code exits. */
    private fun end(tab: ClaudeSessionTab) {
        val process = tab.process ?: return
        process.descendants().forEach { it.destroyForcibly() }
        process.destroyForcibly()
    }

    private fun openThePicker(project: Project): Map<String, Any?> {
        onTheEdt { ClaudeSessionHistoryPicker.open(project) }
        val deadline = System.currentTimeMillis() + 15_000
        while (onTheEdt { ClaudeSessionHistoryPicker.shownIn(project) } == null && System.currentTimeMillis() < deadline) Thread.sleep(100)
        return onTheEdt { describeThePickerIn(project) }
    }

    /** Does [action] to the picker shown in [project]'s window, and answers as `GET /history` then. */
    private fun inThePicker(project: Project, action: (ClaudeSessionHistoryPicker) -> Unit): Map<String, Any?> = onTheEdt {
        action(ClaudeSessionHistoryPicker.shownIn(project)!!)
        describeThePickerIn(project)
    }

    private fun describeThePickerIn(project: Project): Map<String, Any?> {
        val picker = ClaudeSessionHistoryPicker.shownIn(project) ?: return mapOf("shown" to false)
        return mapOf(
            "shown" to true,
            "scope" to picker.scope.name,
            "scopes" to picker.scopes.map { it.name },
            "filter" to picker.filter,
            "selected" to picker.selected,
            "entries" to picker.listed.map { entry ->
                mapOf(
                    "session" to entry.session.id.value,
                    "title" to entry.title,
                    "lastPrompt" to entry.lastPrompt,
                    "folder" to entry.session.workingDirectory.toString(),
                    "gitBranch" to entry.gitBranch,
                    "where" to describe(entry.where),
                    "canBeChosen" to entry.where.canBeChosen,
                )
            },
        )
    }

    private fun describe(where: WhereTheSessionIs): String = when (where) {
        WhereTheSessionIs.NotRunning -> "not-running"
        is WhereTheSessionIs.ExitedInATab -> "exited-in-a-tab"
        is WhereTheSessionIs.RunningInATab -> "running-in-a-tab"
        is WhereTheSessionIs.RunningInAnotherIde -> "running-in-another-ide"
        is WhereTheSessionIs.RunningOutsideClaudeSessions -> "running-outside-claude-sessions"
    }

    private fun actionsBoundTo(keystroke: String, keymapName: String?): Map<String, Any?> {
        val keymap = keymapName?.let { KeymapManager.getInstance().getKeymap(it)!! } ?: KeymapManager.getInstance().activeKeymap
        return mapOf("keymap" to keymap.name, "actions" to keymap.getActionIds(KeyStroke.getKeyStroke(keystroke)!!).toList())
    }

    /** Asynchronous, as the reload it may cause unloads this very channel. */
    private fun refreshThePluginsFolder(): Map<String, Any?> {
        val folder = LocalFileSystem.getInstance().refreshAndFindFileByPath(PathManager.getPluginsPath())
            ?: return mapOf("error" to "The IDE's virtual file system doesn't have the plugins folder ${PathManager.getPluginsPath()}")
        VfsUtil.markDirtyAndRefresh(true, true, true, folder)
        return mapOf("refreshing" to folder.path)
    }

    private fun <T> onTheEdt(compute: () -> T): T {
        var result: T? = null
        ApplicationManager.getApplication().invokeAndWait { result = compute() }
        @Suppress("UNCHECKED_CAST")
        return result as T
    }

    private inline fun <reified T> request(exchange: HttpExchange): T =
        gson.fromJson(exchange.requestBody.readAllBytes().toString(Charsets.UTF_8), T::class.java)

    private fun projectOf(exchange: HttpExchange): Project {
        val open = ProjectManager.getInstance().openProjects
        val name = query(exchange, "project") ?: return open.single()
        return open.single { it.name == name }
    }

    private fun query(exchange: HttpExchange, name: String): String? =
        exchange.requestURI.rawQuery?.split('&')
            ?.map { it.split('=', limit = 2) }
            ?.firstOrNull { it[0] == name }
            ?.let { URLDecoder.decode(it.getOrElse(1) { "" }, Charsets.UTF_8) }

    /**
     * Answers with what [compute] returns, or with the failure: this is where the channel's failures are surfaced, so it
     * catches them all and reports them to the caller.
     */
    private fun answer(exchange: HttpExchange, compute: () -> Map<String, Any?>) {
        val (status, body) = try {
            200 to compute()
        } catch (failure: Throwable) {
            500 to mapOf("error" to failure.toString(), "stackTrace" to failure.stackTrace.take(15).map { it.toString() })
        }
        val bytes = gson.toJson(body).toByteArray(Charsets.UTF_8)
        exchange.responseHeaders.add("Content-Type", "application/json; charset=utf-8")
        exchange.sendResponseHeaders(status, bytes.size.toLong())
        exchange.responseBody.use { it.write(bytes) }
    }

    private class TextRequest(val text: String)

    companion object {
        /** The system property naming the port to listen on. Unset, the channel doesn't listen. */
        const val PORT_PROPERTY = "dev.magnus.claudesessions.observation.port"

        fun getInstance(): ObservationChannel = service()

        /** The keys `/history/press` presses, by name. */
        private val KEYS = mapOf("UP" to KeyEvent.VK_UP, "DOWN" to KeyEvent.VK_DOWN, "ENTER" to KeyEvent.VK_ENTER)
    }
}
