package dev.magnus.claudesessions.observation

import com.google.gson.Gson
import com.google.gson.GsonBuilder
import com.intellij.openapi.Disposable
import com.intellij.openapi.actionSystem.ActionManager
import com.intellij.openapi.actionSystem.ActionPlaces
import com.intellij.openapi.actionSystem.ActionUiKind
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.AnActionResult
import com.intellij.openapi.actionSystem.ex.ActionUtil
import com.intellij.openapi.actionSystem.impl.SimpleDataContext
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.ModalityState
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.project.ProjectManager
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.openapi.vfs.VfsUtil
import com.sun.net.httpserver.HttpExchange
import dev.magnus.claudesessions.windows.DesktopsThroughARestart
import com.sun.net.httpserver.HttpServer
import java.net.InetAddress
import java.net.InetSocketAddress
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors

/**
 * A development-only HTTP server through which the development tools see what the plugin does in the sandbox IDE, and
 * have the IDE reload it. It listens only when the system property [PORT_PROPERTY] names a port, which only the
 * sandbox's `idea.properties` sets, so a user's IDE never has it.
 *
 * Every endpoint answers JSON:
 *  - `GET /state` — when this build of the plugin started, `startedAt`, the projects open, each with its name and
 *    folder and its selected editor, `selectedEditor`: the file it shows, and the line and column of its caret, from 1,
 *    or null when it has none; and the windows it put back on their virtual desktops as their projects opened,
 *    `windowsPutBackOnTheirDesktops`.
 *  - `POST /plugins/refresh` — has the IDE look at its plugins folder again, so that with `idea.auto.reload.plugins` it
 *    reloads a plugin whose files changed, this one included. `/state`'s `startedAt` then tells when the new build's
 *    channel started.
 *  - `POST /actions/perform?id=<an action's id>` — performs the action in the project open, as the user would from a
 *    menu or by its shortcut, and answers once it has been performed: what the action starts in the background may go
 *    on. When the IDE doesn't perform it, as when the action is disabled, it answers with the error.
 *
 * What the plugin has Cloyd show, the tools see in Cloyd's own local API.
 */
@Service(Service.Level.APP)
class ObservationChannel : Disposable {
    private val gson: Gson = GsonBuilder().disableHtmlEscaping().serializeNulls().create()

    /** When this channel started, in milliseconds since the epoch: with each build of the plugin loaded, a new one does. */
    private val startedAt = System.currentTimeMillis()

    private val server: HttpServer? = System.getProperty(PORT_PROPERTY)?.toInt()?.let(::listenOn)

    private fun listenOn(port: Int): HttpServer =
        HttpServer.create(InetSocketAddress(InetAddress.getLoopbackAddress(), port), 0).apply {
            // Each request on a thread of its own: on the server's one thread, a request waiting for the EDT deadlocked a
            // reload of the plugin, whose unloading stops this server on the EDT and waits for that thread.
            executor = Executors.newCachedThreadPool()
            createContext("/state") { exchange -> answer(exchange) { state() } }
            createContext("/plugins/refresh") { exchange -> answer(exchange) { refreshThePluginsFolder() } }
            createContext("/actions/perform") { exchange -> answer(exchange) { perform(idIn(exchange)) } }
            start()
        }

    override fun dispose() {
        server?.stop(0)
        (server?.executor as ExecutorService?)?.shutdown()
    }

    private fun state(): Map<String, Any?> = mapOf(
        "startedAt" to startedAt,
        "projects" to ProjectManager.getInstance().openProjects.map { mapOf("name" to it.name, "basePath" to it.basePath, "selectedEditor" to selectedEditorOf(it)) },
        "windowsPutBackOnTheirDesktops" to DesktopsThroughARestart.windowsPutBackOnTheirDesktops,
    )

    /** The file [project]'s selected editor shows, and where its caret is, as `GET /state` tells it: null when it has none. */
    private fun selectedEditorOf(project: Project): Map<String, Any?>? {
        var selected: Map<String, Any?>? = null
        ApplicationManager.getApplication().invokeAndWait({
            val editor = FileEditorManager.getInstance(project).selectedTextEditor ?: return@invokeAndWait
            val caret = editor.caretModel.logicalPosition
            selected = mapOf("file" to editor.virtualFile?.path, "caretLine" to caret.line + 1, "caretColumn" to caret.column + 1)
        }, ModalityState.any())
        return selected
    }

    private fun refreshThePluginsFolder(): Map<String, Any?> {
        val folder = LocalFileSystem.getInstance().refreshAndFindFileByPath(PathManager.getPluginsPath())
            ?: return mapOf("error" to "The IDE's virtual file system doesn't have the plugins folder ${PathManager.getPluginsPath()}")
        VfsUtil.markDirtyAndRefresh(true, true, true, folder)
        return mapOf("refreshing" to folder.path)
    }

    private fun idIn(exchange: HttpExchange): String =
        exchange.requestURI.query?.split('&')?.firstNotNullOfOrNull { it.removePrefix("id=").takeIf { id -> id != it } }
            ?: error("No action's id in ${exchange.requestURI}: /actions/perform?id=<the action's id>")

    private fun perform(actionId: String): Map<String, Any?> {
        val action = ActionManager.getInstance().getAction(actionId) ?: error("The IDE has no action $actionId.")
        val project = ProjectManager.getInstance().openProjects.single()
        val event = AnActionEvent.createEvent(
            action, SimpleDataContext.getProjectContext(project), null, ActionPlaces.UNKNOWN, ActionUiKind.NONE, null)
        lateinit var result: AnActionResult
        ApplicationManager.getApplication().invokeAndWait {
            ActionUtil.updateAction(action, event)
            result = ActionUtil.performAction(action, event)
        }
        check(result.isPerformed) {
            if (result.isIgnored) "The IDE ignored $actionId: it was disabled, or isn't available while the IDE indexes."
            else "The IDE failed to perform $actionId."
        }
        return mapOf("performed" to actionId, "project" to project.basePath)
    }

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

    companion object {
        /** The system property naming the port to listen on. Unset, the channel doesn't listen. */
        const val PORT_PROPERTY = "dev.magnus.claudesessions.observation.port"

        fun getInstance(): ObservationChannel = service()
    }
}
