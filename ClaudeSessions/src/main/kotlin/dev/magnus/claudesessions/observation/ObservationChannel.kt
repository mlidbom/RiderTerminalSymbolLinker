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
 *  - `GET /tabs/screen?project=&tab=` — the text the tab's terminal shows, from the top of its screen.
 *  - `POST /tabs/type?project=&tab=` with `{"text": …}` — types the text into the tab's terminal, and Enter.
 *  - `POST /tabs/close?project=&tab=` — closes the tab by ending its processes, as its shell's exit does: closing it
 *    as its close button does asks the user to confirm, and that dialog would wait for a human.
 *  - `POST /tabs/close-all?project=` — closes every tab so, and answers once they're gone, so that a specification
 *    starts with none.
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
            createContext("/tabs/screen") { exchange -> answer(exchange) { screen(projectOf(exchange), query(exchange, "tab")!!.toInt()) } }
            createContext("/tabs/close-all") { exchange -> answer(exchange) { closeEveryTab(projectOf(exchange)) } }
            createContext("/tabs/close") { exchange -> answer(exchange) { close(projectOf(exchange), query(exchange, "tab")!!.toInt()) } }
            createContext("/plugins/refresh") { exchange -> answer(exchange) { refreshThePluginsFolder() } }
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
                "saved" to mapOf("file" to saved.location.toString(), "sessions" to saved.read().map(::describe)),
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
    }
}
