package dev.magnus.claudesessions.api

import com.google.gson.Gson
import com.intellij.openapi.Disposable
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.ModalityState
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.ProjectManager
import com.sun.net.httpserver.HttpExchange
import com.sun.net.httpserver.HttpServer
import dev.magnus.claudesessions.ClaudeSessionId
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.ClaudeSessionsView
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.URLDecoder
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors

/**
 * Claude Sessions' API for other tools on the same machine: an HTTP server that listens on a free port of the loopback
 * address only, and that the IDE's [LocalApiAnnouncement] names. Its requests:
 *
 *  - `GET /sessions`: the sessions that run in the IDE's tabs, as `{"sessions": [{"session": <session id>, "process":
 *    <Claude Code's process id>}]}`. Another IDE's session history picker asks, to tell a session that runs in a tab
 *    from one that runs elsewhere, as in a plain terminal.
 *  - `POST /sessions/show?session=<session id>&process=<Claude Code's process id>`: brings forward the tab that runs the
 *    session, as clicking a notification about it would want. It finds the tab in which that Claude Code runs, or else
 *    the one that runs the session, or ran it last: `process` finds a tab after `/clear` gave it a new session, and
 *    `session` finds one whose Claude Code was resumed in a new process. Either may be left out. It answers 200 with
 *    `{"shown": true}`, or 404 with `{"shown": false}` when none of this IDE's tabs runs the session.
 *
 * A tool whose notification about a session the user clicks calls it, after it has brought the IDE's window forward
 * itself: Windows lets the process the user clicked bring a window forward, and not the IDE.
 *
 * It has no authentication: any process on the machine can reach a loopback port, and showing a tab is harmless. A
 * request that changes something, such as typing into a session, would need a token in the announcement, required with
 * each request.
 */
@Service(Service.Level.APP)
class LocalApi : Disposable {
    private val gson = Gson()

    private val server: HttpServer =
        HttpServer.create(InetSocketAddress(InetAddress.getLoopbackAddress(), 0), 0).apply {
            // Each request on a thread of its own, as they wait for the EDT, which may wait for this server to stop.
            executor = Executors.newCachedThreadPool()
            createContext("/sessions/show") { exchange -> answer(exchange) }
            createContext("/sessions") { exchange -> answerWithTheSessions(exchange) }
            start()
        }

    private val announcement = LocalApiAnnouncement.of(ProcessHandle.current()).also { it.announce(server.address.port) }

    override fun dispose() {
        announcement.withdraw()
        server.stop(0)
        (server.executor as ExecutorService).shutdown()
    }

    private fun answer(exchange: HttpExchange) = reply(exchange) {
        if (exchange.requestMethod != "POST") 405 to mapOf("error" to "POST it")
        else if (show(queryOf(exchange))) 200 to mapOf("shown" to true)
        else 404 to mapOf("shown" to false)
    }

    private fun answerWithTheSessions(exchange: HttpExchange) = reply(exchange) {
        val sessions = onTheEdt { ProjectManager.getInstance().openProjects.flatMap { ClaudeSessionsView.getInstance(it).tabs } }
            .mapNotNull { it.claudeCode }
            .map { mapOf("session" to it.session.id.value, "process" to it.process.pid()) }
        200 to mapOf("sessions" to sessions)
    }

    /** Replies to [exchange] with the status and the body, as JSON, that [compute] returns. */
    private fun reply(exchange: HttpExchange, compute: () -> Pair<Int, Map<String, Any>>) = exchange.use {
        val (status, body) = compute()
        val bytes = gson.toJson(body).toByteArray()
        exchange.responseHeaders.add("Content-Type", "application/json; charset=utf-8")
        exchange.sendResponseHeaders(status, bytes.size.toLong())
        exchange.responseBody.write(bytes)
    }

    /** Brings forward the tab the [query] names, and returns whether one of this IDE's tabs runs it. */
    private fun show(query: Map<String, String>): Boolean {
        val process = query["process"]?.toLongOrNull()
        val session = query["session"]?.let(::ClaudeSessionId)
        val views = onTheEdt { ProjectManager.getInstance().openProjects.map(ClaudeSessionsView::getInstance).associateWith { it.tabs } }
        val found = views.entries.firstNotNullOfOrNull { (view, tabs) -> tabs.firstOrNull { it.runsClaudeCode(process) }?.let { view to it } }
            ?: views.entries.firstNotNullOfOrNull { (view, tabs) -> tabs.firstOrNull { it.runs(session) }?.let { view to it } }
            ?: return false
        val (view, tab) = found
        onTheEdt { view.bringForward(tab) }
        return true
    }

    private fun ClaudeSessionTab.runsClaudeCode(process: Long?): Boolean = process != null && claudeCode?.process?.pid() == process

    private fun ClaudeSessionTab.runs(session: ClaudeSessionId?): Boolean = session != null && lookAtTheSession()?.id == session

    /** Runs [action] on the EDT, whatever dialog is open: the user asked for the tab, from outside the IDE. */
    private fun <T> onTheEdt(action: () -> T): T {
        var result: Result<T>? = null
        ApplicationManager.getApplication().invokeAndWait({ result = runCatching(action) }, ModalityState.any())
        return result!!.getOrThrow()
    }

    private fun queryOf(exchange: HttpExchange): Map<String, String> =
        exchange.requestURI.rawQuery.orEmpty().split('&').filter { '=' in it }.associate {
            val (name, value) = it.split('=', limit = 2)
            URLDecoder.decode(name, Charsets.UTF_8) to URLDecoder.decode(value, Charsets.UTF_8)
        }

    companion object {
        /** Starts the API, unless it runs already: the first project to open starts it. */
        fun start() {
            service<LocalApi>()
        }
    }
}
