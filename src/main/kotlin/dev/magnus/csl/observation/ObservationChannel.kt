package dev.magnus.csl.observation

import com.google.gson.Gson
import com.google.gson.GsonBuilder
import com.intellij.execution.filters.HyperlinkInfo
import com.intellij.execution.filters.HyperlinkInfoBase
import com.intellij.openapi.Disposable
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.project.ProjectManager
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.openapi.vfs.VfsUtil
import com.intellij.openapi.wm.WindowManager
import com.intellij.ui.awt.RelativePoint
import com.sun.net.httpserver.HttpExchange
import com.sun.net.httpserver.HttpServer
import dev.magnus.csl.FileHyperlinkInfo
import dev.magnus.csl.LinkClickOutcome
import dev.magnus.csl.LinkClickOutcomes
import dev.magnus.csl.NamespaceHyperlinkInfo
import dev.magnus.csl.SolutionFileNames
import dev.magnus.csl.SymbolHyperlinkInfo
import dev.magnus.csl.SymbolIndex
import dev.magnus.csl.SymbolLinkFilterProvider
import java.awt.Point
import java.awt.image.BufferedImage
import java.io.File
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.URLDecoder
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors
import java.util.concurrent.CompletableFuture
import java.util.concurrent.TimeUnit
import java.util.concurrent.TimeoutException
import java.util.concurrent.atomic.AtomicReference
import javax.imageio.ImageIO

/**
 * A development-only HTTP server through which the development tools see what the plugin does in the sandbox Rider:
 * the state of its indexes, the links its filters make in a line of terminal output, and what a click on one of them
 * does. It listens only when the system property [PORT_PROPERTY] names a port, which only the sandbox Rider's
 * `idea.properties` sets, so a user's Rider never has it.
 *
 * Every endpoint answers JSON. `project` names the open project to ask about, and may be left out when one is open:
 *  - `GET /state` — each open project's [SymbolIndex] and [SolutionFileNames]: ready, and how much they hold.
 *  - `POST /links?project=` with `{"line": …}` — the links the plugin's filters make in the line.
 *  - `POST /click?project=` with `{"line": …, "link": …}` — clicks the link whose text is `link`, and answers with
 *    the [LinkClickOutcome], and for an opened file, where the editor is. A link the terminal tells where it was
 *    clicked is clicked at [CLICK_POINT] in the project's window, and the answer says where that is on the screen.
 *  - `POST /picker/choose?project=&row=` — chooses a row of the picker the last click showed, as Enter does.
 *  - `POST /picker/close` — closes that picker.
 *  - `POST /picker/picture?file=` — paints that picker, as it shows, into the PNG file `file`, and leaves it open.
 *  - `POST /editors/close-all?project=` — closes every editor, so that a click opens its file afresh.
 *  - `POST /plugins/refresh` — has Rider look at its plugins folder again, so that with `idea.auto.reload.plugins`
 *    it reloads a plugin whose files changed, this one included. Nothing else makes Rider look there, but activating
 *    its window. `/state`'s `startedAt` then tells when the new build's channel started.
 */
@Service(Service.Level.APP)
class ObservationChannel : Disposable {
    private val gson: Gson = GsonBuilder().disableHtmlEscaping().serializeNulls().create()

    @Volatile
    private var shownPicker: LinkClickOutcome.PickerShown? = null

    /** When this channel started, in milliseconds since the epoch: with each build of the plugin loaded, a new one does. */
    private val startedAt = System.currentTimeMillis()

    private val server: HttpServer? = System.getProperty(PORT_PROPERTY)?.toInt()?.let(::listenOn)

    private fun listenOn(port: Int): HttpServer =
        HttpServer.create(InetSocketAddress(InetAddress.getLoopbackAddress(), port), 0).apply {
            // Each request on a thread of its own: on the server's one thread, a request waiting for the EDT deadlocks a
            // reload of the plugin, whose unloading stops this server on the EDT and waits for that thread.
            executor = Executors.newCachedThreadPool()
            createContext("/state") { exchange -> answer(exchange) { state() } }
            createContext("/links") { exchange -> answer(exchange) { links(projectOf(exchange), request<LineRequest>(exchange).line) } }
            createContext("/click") { exchange ->
                answer(exchange) {
                    val click = request<ClickRequest>(exchange)
                    click(projectOf(exchange), click.line, click.link)
                }
            }
            createContext("/picker/choose") { exchange -> answer(exchange) { choose(projectOf(exchange), query(exchange, "row")!!.toInt()) } }
            createContext("/picker/close") { exchange -> answer(exchange) { closePicker() } }
            createContext("/picker/picture") { exchange -> answer(exchange) { pictureOfThePicker(File(query(exchange, "file")!!)) } }
            createContext("/plugins/refresh") { exchange -> answer(exchange) { refreshThePluginsFolder() } }
            createContext("/editors/close-all") { exchange -> answer(exchange) { closeEveryEditor(projectOf(exchange)) } }
            start()
        }

    override fun dispose() {
        server?.stop(0)
        (server?.executor as ExecutorService?)?.shutdown()
        LinkClickOutcomes.observer = null
    }

    private fun closeEveryEditor(project: Project): Map<String, Any?> {
        ApplicationManager.getApplication().invokeAndWait {
            val editors = FileEditorManager.getInstance(project)
            editors.openFiles.forEach(editors::closeFile)
        }
        return mapOf("closed" to true)
    }

    /** Asynchronous, as the reload it may cause unloads this very channel. */
    private fun refreshThePluginsFolder(): Map<String, Any?> {
        val folder = LocalFileSystem.getInstance().refreshAndFindFileByPath(PathManager.getPluginsPath())
            ?: return mapOf("error" to "Rider's virtual file system doesn't have the plugins folder ${PathManager.getPluginsPath()}")
        VfsUtil.markDirtyAndRefresh(true, true, true, folder)
        return mapOf("refreshing" to folder.path)
    }

    private fun state(): Map<String, Any?> = mapOf(
        "startedAt" to startedAt,
        "projects" to ProjectManager.getInstance().openProjects.map { project ->
            val symbols = SymbolIndex.getInstance(project)
            val files = SolutionFileNames.getInstance(project)
            mapOf(
                "name" to project.name,
                "basePath" to project.basePath,
                "symbolIndex" to mapOf("ready" to symbols.isReady, "typesAndMembers" to symbols.declared.typesAndMembers.size, "namespaces" to symbols.declared.namespaces.size),
                "fileIndex" to mapOf("ready" to files.isReady, "files" to files.fileCount),
            )
        },
    )

    private fun links(project: Project, line: String): Map<String, Any?> =
        mapOf("links" to TerminalLine(project, line).links.map { it.description })

    private fun click(project: Project, line: String, linkText: String): Map<String, Any?> {
        val links = TerminalLine(project, line).links
        val link = links.firstOrNull { it.text == linkText }
            ?: return mapOf("outcome" to "noSuchLink", "links" to links.map { it.description })
        val clickedAt = AtomicReference<Point?>()
        val outcome = outcomeOf(project) {
            when (val hyperlinkInfo = link.hyperlinkInfo) {
                is HyperlinkInfoBase -> {
                    val where = RelativePoint(WindowManager.getInstance().getFrame(project)!!.rootPane, Point(CLICK_POINT))
                    clickedAt.set(where.screenPoint)
                    hyperlinkInfo.navigate(project, where)
                }
                else -> hyperlinkInfo.navigate(project)
            }
        }
        return outcome + ("clickedAt" to clickedAt.get()?.let(::described))
    }

    private fun choose(project: Project, row: Int): Map<String, Any?> {
        val picker = shownPicker ?: return mapOf("outcome" to "noPickerShown")
        shownPicker = null
        return outcomeOf(project) { picker.choose(row) }
    }

    private fun closePicker(): Map<String, Any?> {
        val picker = shownPicker ?: return mapOf("outcome" to "noPickerShown")
        shownPicker = null
        ApplicationManager.getApplication().invokeAndWait { picker.close() }
        return mapOf("outcome" to "closed")
    }

    private fun pictureOfThePicker(file: File): Map<String, Any?> {
        val picker = shownPicker ?: return mapOf("outcome" to "noPickerShown")
        var picture: BufferedImage? = null
        ApplicationManager.getApplication().invokeAndWait { picture = picker.picture() }
        ImageIO.write(picture!!, "png", file)
        return mapOf("wrote" to file.path, "width" to picture!!.width, "height" to picture!!.height)
    }

    /** Runs [action] on the EDT, as the terminal runs a click, and waits for the [LinkClickOutcome] it reports. */
    private fun outcomeOf(project: Project, action: () -> Unit): Map<String, Any?> {
        val reported = CompletableFuture<LinkClickOutcome>()
        LinkClickOutcomes.observer = { reported.complete(it) }
        try {
            ApplicationManager.getApplication().invokeLater(action)
            val outcome = try {
                reported.get(CLICK_TIMEOUT_SECONDS, TimeUnit.SECONDS)
            } catch (_: TimeoutException) {
                return mapOf("outcome" to "nothingReported", "waitedSeconds" to CLICK_TIMEOUT_SECONDS)
            }
            return describe(project, outcome)
        } finally {
            LinkClickOutcomes.observer = null
        }
    }

    private fun describe(project: Project, outcome: LinkClickOutcome): Map<String, Any?> = when (outcome) {
        is LinkClickOutcome.Opened -> mapOf(
            "outcome" to "opened",
            "target" to mapOf("file" to outcome.file, "line" to outcome.line),
            "editor" to SelectedEditor.onceItShows(project, outcome.file),
        )
        is LinkClickOutcome.PickerShown -> {
            shownPicker = outcome
            mapOf("outcome" to "picker", "title" to outcome.title, "note" to outcome.note, "location" to described(outcome.location), "rows" to outcome.rows)
        }
        is LinkClickOutcome.NothingFound -> mapOf("outcome" to "nothingFound", "message" to outcome.message)
        is LinkClickOutcome.FellBackToSearchEverywhere -> mapOf("outcome" to "searchEverywhere", "token" to outcome.token)
    }

    private fun described(point: Point): Map<String, Int> = mapOf("x" to point.x, "y" to point.y)

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

    private class LineRequest(val line: String)

    private class ClickRequest(val line: String, val link: String)

    companion object {
        /** The system property naming the port to listen on. Unset, the channel doesn't listen. */
        const val PORT_PROPERTY = "dev.magnus.csl.observation.port"

        private const val CLICK_TIMEOUT_SECONDS = 20L

        /** Where in the project's window a link is clicked: away from its edges, so that a picker opened there fits. */
        private val CLICK_POINT = Point(200, 150)

        fun getInstance(): ObservationChannel = service()
    }
}

/** A line of terminal output, with the links the plugin's filters make in it, as the terminal would get them. */
private class TerminalLine(project: Project, private val line: String) {
    val links: List<Link> = SymbolLinkFilterProvider().getDefaultFilters(project).flatMap { filter ->
        filter.applyFilter(line, line.length)?.resultItems.orEmpty().map {
            Link(line.substring(it.highlightStartOffset, it.highlightEndOffset), it.highlightStartOffset, it.hyperlinkInfo!!)
        }
    }.sortedBy { it.start }

    class Link(val text: String, val start: Int, val hyperlinkInfo: HyperlinkInfo) {
        val description: Map<String, Any?>
            get() = mapOf(
                "text" to text,
                "start" to start,
                "kind" to when (hyperlinkInfo) {
                    is SymbolHyperlinkInfo -> "symbol"
                    is NamespaceHyperlinkInfo -> "namespace"
                    is FileHyperlinkInfo -> "file"
                    else -> hyperlinkInfo.javaClass.name
                },
            )
    }
}

/** Where the project's selected editor is: its file, the caret's line, and the lines a selection covers. */
private object SelectedEditor {
    private const val HOW_LONG_OPENING_MAY_TAKE_MILLISECONDS = 5_000L

    /**
     * Where the selected editor is once it shows [file], or after a few seconds if it doesn't. Rider opens an editor for
     * a file that isn't open yet asynchronously, after the click has returned, and puts the caret where it was asked to
     * once the editor has opened.
     */
    fun onceItShows(project: Project, file: String): Map<String, Any?>? {
        val deadline = System.currentTimeMillis() + HOW_LONG_OPENING_MAY_TAKE_MILLISECONDS
        var description = of(project)
        while (!description.isOf(file) && System.currentTimeMillis() < deadline) {
            Thread.sleep(100)
            description = of(project)
        }
        Thread.sleep(300) // The caret is put in place once the editor has opened.
        return of(project)
    }

    private fun Map<String, Any?>?.isOf(file: String): Boolean =
        (this?.get("file") as String?)?.equals(file.replace('\\', '/'), ignoreCase = true) == true

    private fun of(project: Project): Map<String, Any?>? {
        var description: Map<String, Any?>? = null
        ApplicationManager.getApplication().invokeAndWait {
            val editor = FileEditorManager.getInstance(project).selectedTextEditor ?: return@invokeAndWait
            val document = editor.document
            val selection = editor.selectionModel
            description = mapOf(
                "file" to FileDocumentManager.getInstance().getFile(document)?.path,
                "caretLine" to editor.caretModel.logicalPosition.line + 1,
                "caretColumn" to editor.caretModel.logicalPosition.column + 1,
                "selectedLines" to if (selection.hasSelection()) {
                    listOf(document.getLineNumber(selection.selectionStart) + 1, document.getLineNumber(selection.selectionEnd) + 1)
                } else {
                    null
                },
            )
        }
        return description
    }
}
