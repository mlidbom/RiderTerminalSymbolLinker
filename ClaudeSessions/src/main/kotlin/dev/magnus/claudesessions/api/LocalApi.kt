package dev.magnus.claudesessions.api

import com.google.gson.GsonBuilder
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.intellij.openapi.Disposable
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.ModalityState
import com.intellij.openapi.application.ex.ApplicationManagerEx
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.diagnostic.thisLogger
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.ProjectManager
import com.intellij.openapi.vfs.LocalFileSystem
import com.sun.net.httpserver.HttpExchange
import com.sun.net.httpserver.HttpServer
import dev.magnus.claudesessions.windows.DesktopsThroughARestart
import dev.magnus.claudesessions.windows.ProjectWindow
import java.net.InetAddress
import java.nio.file.Path
import java.net.InetSocketAddress
import java.security.SecureRandom
import java.util.HexFormat
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors

/**
 * Claude Sessions' API for other tools on the same machine: an HTTP server that listens on a free port of the loopback
 * address only, and that the IDE's [LocalApiAnnouncement] names. Its requests:
 *
 *  - `GET /windows`: the windows of the projects open in the IDE, as `{"windows": [{"project": <the project's folder>,
 *    "title": <the window's title>, "window": <its native handle, HWND>, "desktop": <the GUID of the virtual desktop it's
 *    on, or null>}]}`.
 *  - `POST /restart` with `{"why": <why>, "askedBy": <who asked>, "beforeItStartsAgain": <a command, as an array of
 *    its program and arguments, or left out>}`, and the announcement's token: has the IDE restart itself, as after a
 *    plugin's update, without asking the user to confirm it. It answers 202 once the restart is on its way, or 409 with
 *    `{"error": <why not>}` when a modal dialog is open. The IDE may still ask on the way, as whether to end the
 *    processes that run, and the user may cancel it. Each project's window goes back to its virtual desktop once the
 *    project has opened again, as after every restart ([DesktopsThroughARestart]). The hub's restart of the IDEs asks,
 *    which the tooling owner builds (`~/.claude/dev_docs/restarting-the-ides.md`).
 *    `beforeItStartsAgain` runs once the IDE has exited, and the IDE starts again once it has ended, as the IDE's updater
 *    applies a patch ([RestartRunningACommand]): the development tools put a build of a plugin in so, which a running
 *    IDE couldn't take.
 *  - `POST /files/open` with `{"project": <the project's folder>, "file": <a file's full path>, "line": <from 1, or left
 *    out>, "column": <from 1, or left out>}`, and the announcement's token: opens the file in an editor of that
 *    project's window, at the line and column, with the IDE's focus in it, as a click on a link in Cloyd's chat asks.
 *    The file may be outside the project, as a worktree's is. It answers 200 with `{"window": <the window's native
 *    handle>}`, which the asker brings forward itself: the IDE may not take the foreground from it. 404 with
 *    `{"error": <why>}` when no window of the IDE shows the project, or no file is at the path.
 *
 * Any process on the machine can reach a loopback port, so a request that changes something, as `POST /restart` does,
 * must carry the announcement's token, `Authorization: Bearer <token>`, new at each start of the IDE; it's answered 401
 * without it. What is only read is harmless, and needs none. Every request must name the API's own
 * address as its `Host`, else it's answered 403: a web page can't then reach it through a name of its own that resolves
 * to the loopback address.
 */
@Service(Service.Level.APP)
class LocalApi : Disposable {
    private val gson = GsonBuilder().serializeNulls().disableHtmlEscaping().create()

    /** What a request that changes something must carry: see the API's description. */
    private val token: String = HexFormat.of().formatHex(ByteArray(32).also(SecureRandom()::nextBytes))

    private val server: HttpServer =
        HttpServer.create(InetSocketAddress(InetAddress.getLoopbackAddress(), 0), 0).apply {
            // Each request on a thread of its own, as they wait for the EDT, which may wait for this server to stop.
            executor = Executors.newCachedThreadPool()
            createContext("/windows") { exchange -> ifItsHostIsThisApi(exchange, ::answerWithTheWindows) }
            createContext("/restart") { exchange -> ifItsHostIsThisApi(exchange, ::restart) }
            createContext("/files/open") { exchange -> ifItsHostIsThisApi(exchange, ::openAFile) }
            start()
        }

    private val announcement = LocalApiAnnouncement.of(ProcessHandle.current()).also { it.announce(server.address.port, token) }

    override fun dispose() {
        announcement.withdraw()
        server.stop(0)
        (server.executor as ExecutorService).shutdown()
    }

    private fun answerWithTheWindows(exchange: HttpExchange) = reply(exchange) {
        val windows = onTheEdt { ProjectWindow.ofTheOpenProjects() }.map {
            mapOf("project" to it.project.toString(), "title" to it.title, "window" to it.handle.value, "desktop" to it.desktop?.value?.toString())
        }
        200 to mapOf("windows" to windows)
    }

    /** Has the IDE restart itself, once it has answered, unless it can't now: see the API's description. */
    private fun restart(exchange: HttpExchange) {
        var beforeItStartsAgain: List<String>? = null
        reply(exchange) {
            when {
                exchange.requestMethod != "POST" -> 405 to mapOf("error" to "POST it")
                !carriesTheToken(exchange) -> 401 to mapOf("error" to "A restart must carry the token of the IDE's announcement, as Authorization: Bearer <token>.")
                else -> {
                    val request = JsonParser.parseString(exchange.requestBody.readAllBytes().decodeToString()).asJsonObject
                    val whyNot = onTheEdt { whyTheIdeCannotRestartNow() }
                    if (whyNot != null) 409 to mapOf("error" to whyNot)
                    else {
                        beforeItStartsAgain = request.getAsJsonArray("beforeItStartsAgain")?.map { it.asString }.orEmpty()
                        thisLogger().info("Restarting the IDE, as ${request.textOf("askedBy")} asked: ${request.textOf("why")}. Before it starts again: $beforeItStartsAgain")
                        202 to mapOf("restarting" to true)
                    }
                }
            }
        }
        beforeItStartsAgain?.let { command -> ApplicationManager.getApplication().invokeLater({ restartTheIde(command) }, ModalityState.nonModal()) }
    }

    /** Opens a file in an editor of a project's window: see the API's description. */
    private fun openAFile(exchange: HttpExchange) = reply(exchange) {
        when {
            exchange.requestMethod != "POST" -> 405 to mapOf("error" to "POST it")
            !carriesTheToken(exchange) -> 401 to mapOf("error" to "Opening a file must carry the token of the IDE's announcement, as Authorization: Bearer <token>.")
            else -> {
                val request = JsonParser.parseString(exchange.requestBody.readAllBytes().decodeToString()).asJsonObject
                val folder = Path.of(request.get("project").asString)
                val path = request.get("file").asString
                val line = request.numberOrNullOf("line")
                val column = request.numberOrNullOf("column")
                onTheEdt {
                    val project = ProjectManager.getInstance().openProjects.firstOrNull { it.basePath?.let(Path::of) == folder }
                    val window = project?.let(ProjectWindow::of)
                    val file = LocalFileSystem.getInstance().refreshAndFindFileByPath(path.replace('\\', '/'))
                    when {
                        project == null || window == null -> 404 to mapOf("error" to "No window of the IDE shows the project $folder.")
                        file == null -> 404 to mapOf("error" to "No file is at $path.")
                        else -> {
                            (if (line == null) OpenFileDescriptor(project, file) else OpenFileDescriptor(project, file, line - 1, (column ?: 1) - 1)).navigate(true)
                            200 to mapOf("window" to window.handle.value)
                        }
                    }
                }
            }
        }
    }

    private fun JsonObject.numberOrNullOf(name: String): Int? = get(name)?.takeUnless { it.isJsonNull }?.asInt

    /**
     * Has the IDE restart itself, as after a plugin's update, running the command [beforeItStartsAgain] between the IDE's
     * exit and its start, unless it's empty: see [RestartRunningACommand].
     */
    private fun restartTheIde(beforeItStartsAgain: List<String>) {
        if (beforeItStartsAgain.isEmpty()) ApplicationManagerEx.getApplicationEx().restart(true)
        else RestartRunningACommand(beforeItStartsAgain).start()
    }

    /**
     * Why the IDE can't restart now, or null when it can. On the EDT. One that exits already exits: asked to restart
     * then, it goes on as it was.
     */
    private fun whyTheIdeCannotRestartNow(): String? =
        "A modal dialog is open in the IDE: it would wait for the user.".takeIf { ModalityState.current() != ModalityState.nonModal() }

    private fun JsonObject.textOf(name: String): String = get(name)?.takeUnless { it.isJsonNull }?.asString ?: "(not said)"

    private fun carriesTheToken(exchange: HttpExchange): Boolean = exchange.requestHeaders.getFirst("Authorization") == "Bearer $token"

    /**
     * Has [answer] answer [exchange] if it names this API's own address as its `Host`, and else answers 403: see the
     * API's description.
     */
    private fun ifItsHostIsThisApi(exchange: HttpExchange, answer: (HttpExchange) -> Unit) {
        val port = server.address.port
        if (exchange.requestHeaders.getFirst("Host") in setOf("127.0.0.1:$port", "localhost:$port")) answer(exchange)
        else reply(exchange) { 403 to mapOf("error" to "The request's Host must be this API's own address, 127.0.0.1:$port.") }
    }

    /** Replies to [exchange] with the status and the body, as JSON, that [compute] returns. */
    private fun reply(exchange: HttpExchange, compute: () -> Pair<Int, Map<String, Any?>>) = exchange.use {
        val (status, body) = compute()
        val bytes = gson.toJson(body).toByteArray()
        exchange.responseHeaders.add("Content-Type", "application/json; charset=utf-8")
        exchange.sendResponseHeaders(status, bytes.size.toLong())
        exchange.responseBody.write(bytes)
    }

    /** Runs [action] on the EDT, whatever dialog is open: another tool asks, from outside the IDE. */
    private fun <T> onTheEdt(action: () -> T): T {
        var result: Result<T>? = null
        ApplicationManager.getApplication().invokeAndWait({ result = runCatching(action) }, ModalityState.any())
        return result!!.getOrThrow()
    }

    companion object {
        /** Starts the API, unless it runs already: the first project to open starts it. */
        fun start() {
            service<LocalApi>()
        }
    }
}
