package dev.magnus.claudesessions.observation

import com.google.gson.Gson
import com.google.gson.GsonBuilder
import com.google.gson.JsonObject
import com.intellij.openapi.Disposable
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.keymap.KeymapManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.project.ProjectManager
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.openapi.vfs.VfsUtil
import com.intellij.terminal.frontend.toolwindow.TerminalToolWindowTabsManager
import com.sun.net.httpserver.HttpExchange
import com.sun.net.httpserver.HttpServer
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.history.ClaudeSessionHistoryPicker
import dev.magnus.claudesessions.history.ClaudeSessionHistoryScope
import dev.magnus.claudesessions.history.WhereTheSessionIs
import dev.magnus.claudesessions.hub.ClaudeToolingHub
import dev.magnus.claudesessions.newsession.NewClaudeSessionPopup
import dev.magnus.claudesessions.saving.ClaudeSessionKeeper
import dev.magnus.claudesessions.saving.SavedClaudeSessions
import dev.magnus.claudesessions.saving.SavedTab
import java.awt.event.KeyEvent
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.URLDecoder
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors
import javax.swing.KeyStroke
import kotlinx.coroutines.runBlocking
import org.jetbrains.plugins.terminal.TerminalProjectOptionsProvider

/**
 * A development-only HTTP server through which the development tools see and drive what the plugin does in the
 * sandbox IDE. It listens only when the system property [PORT_PROPERTY] names a port, which only the sandbox's
 * `idea.properties` sets, so a user's IDE never has it.
 *
 * Every endpoint answers JSON. `project` names the open project to ask about, and may be left out when one is open:
 *  - `GET /state` — each open project's [ClaudeSessionsView]: its tabs, each with the process its terminal runs, its
 *    session host's attach client, its session host and the host's shell, the Claude Code that runs in it and that
 *    one's parent process, and its session; the [SavedClaudeSessions], each tab with its session and its host, and
 *    whether the [ClaudeSessionKeeper] keeps them; and the Claude Codes the [ClaudeToolingHub] last told of, each with
 *    its process, its session and its session host.
 *  - `POST /view/open?project=` — has the IDE give the view its content, as showing the tool window would.
 *  - `POST /sessions/new?project=` — starts a new session in a tab, without a name, as the New Claude Session popup
 *    does with its field left empty, and answers with the tab's place among the view's tabs once its session host
 *    has started.
 *  - `POST /new-session-popup/open?project=` — opens the New Claude Session popup, as the New Claude Session action
 *    does, and answers as `GET /new-session-popup`.
 *  - `GET /new-session-popup?project=` — whether the popup shows in the project's window, and the name typed into it.
 *  - `POST /new-session-popup/name?project=` with `{"text": …}` — types the name into the popup's field.
 *  - `POST /new-session-popup/enter?project=` — has the popup's field take Enter, which starts the session, and
 *    answers with its tab's place among the view's tabs once the tab has opened, as `/sessions/new` does.
 *  - `POST /new-session-popup/close?project=` — closes the popup, as Escape does.
 *  - `POST /terminal/shell?project=&path=` — has the project's terminals run the shell `path` from now on, as its
 *    setting in the IDE's terminal settings does, such as `cmd.exe`, and answers with the one they ran.
 *  - `POST /tabs/new-plain?project=` — opens a plain terminal tab in the view's tool window, not through the view, as
 *    a tab can come into it that the view didn't open, and answers with its place among the view's tabs.
 *  - `POST /tabs/select?project=&tab=` — selects the tab, and shows the tool window, as a click on the tab does.
 *  - `GET /tabs/screen?project=&tab=` — the text the tab's terminal shows, from the top of its screen, and the size
 *    of its screen, in columns and rows, once the tab has been laid out.
 *  - `POST /tabs/type?project=&tab=` with `{"text": …}` — types the text into the tab's terminal, and Enter.
 *  - `POST /tabs/close?project=&tab=` — closes the tab, as its close button does: its session ends with it.
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
 *
 * A tab's chat, through [TheChatObserved], each answering as `GET /tabs/chat` does:
 *  - `GET /tabs/chat?project=&tab=` — the card shown, or null for a tab without a chat, and the card the user chose; the
 *    conversation's text, its folds, its links, and the item at the top of the pane; the status line; the text in the
 *    input box, part by part, and its caret; and the titles of the cards' tabs.
 *  - `POST /tabs/chat/conversation?project=&tab=` with `{"items": [...]}` — has the tab's stand-in give the chat that
 *    conversation: see [TheChatObserved.giveTheConversation].
 *  - `POST /tabs/chat/status?project=&tab=&status=` — has the stand-in say Claude Code's status, such as `IDLE`.
 *  - `GET /tabs/chat/sent?project=&tab=` — the messages Claude Code got from the chat, part by part, and the keys.
 *  - `POST /tabs/chat/outcome?project=&tab=` with `{"outcome": …}` — has the stand-in answer what the chat gives it with
 *    that outcome, from then on: see [TheChatObserved.answerWith].
 *  - `POST /tabs/chat/hold?project=&tab=&hold=` — has the stand-in hold what the chat gives it, or let it go.
 *  - `POST /tabs/chat/type?project=&tab=` with `{"text": …}` — types the text into the input box.
 *  - `POST /tabs/chat/paste?project=&tab=` with `{"text": …}` — pastes the text into it, as from the clipboard.
 *  - `POST /tabs/chat/paste-image?project=&tab=` — pastes a small image into it, as from the clipboard.
 *  - `POST /tabs/chat/press?project=&tab=&key=` — presses `ENTER`, `SHIFT_ENTER`, `ESCAPE` or `SHIFT_TAB` in it.
 *  - `POST /tabs/chat/switch?project=&tab=` — switches between the chat and the terminal, as the action does.
 *  - `POST /tabs/chat/click-tab?project=&tab=&card=` — shows the card, `CHAT` or `TERMINAL`, as clicking its tab does.
 *  - `GET /tabs/chat/shortcut?project=&tab=&keystroke=&in=` — the action the IDE would run for the keystroke, such as
 *    `ctrl H`, pressed `in` the chat, the terminal, or `elsewhere`: see [TheChatObserved.theActionTheShortcutRuns].
 *  - `POST /tabs/chat/fold?project=&tab=&fold=&open=` — opens a fold of the conversation, by its place, or closes it.
 *  - `POST /tabs/chat/scroll?project=&tab=&item=` — scrolls the conversation so that the item, by its id, is at the top.
 *  - `POST /chats/behind?by=` — has the tabs opened from then on get `standIn`, a [StandInBehindTheChat], behind their
 *    chats, as they do unless asked otherwise, or the `hub`'s adapter, as outside the sandbox.
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
            createContext("/new-session-popup/open") { exchange -> answer(exchange) { openTheNewSessionPopup(projectOf(exchange)) } }
            createContext("/new-session-popup/name") { exchange ->
                answer(exchange) { inTheNewSessionPopup(projectOf(exchange)) { it.name = request<TextRequest>(exchange).text } }
            }
            createContext("/new-session-popup/enter") { exchange -> answer(exchange) { pressEnterInTheNewSessionPopup(projectOf(exchange)) } }
            createContext("/new-session-popup/close") { exchange -> answer(exchange) { inTheNewSessionPopup(projectOf(exchange)) { it.close() } } }
            createContext("/new-session-popup") { exchange -> answer(exchange) { onTheEdt { describeTheNewSessionPopupIn(projectOf(exchange)) } } }
            createContext("/terminal/shell") { exchange -> answer(exchange) { runTheShell(projectOf(exchange), query(exchange, "path")!!) } }
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
            createContext("/tabs/chat/conversation") { exchange ->
                answer(exchange) {
                    val conversation = request<JsonObject>(exchange)
                    inTheChat(exchange) { it.giveTheConversation(conversation) }
                }
            }
            createContext("/tabs/chat/status") { exchange -> answer(exchange) { inTheChat(exchange) { it.sayTheStatus(query(exchange, "status")!!) } } }
            createContext("/tabs/chat/sent") { exchange -> answer(exchange) { inTheChat(exchange) { it.sent() } } }
            createContext("/tabs/chat/outcome") { exchange ->
                answer(exchange) {
                    val outcome = request<JsonObject>(exchange)
                    inTheChat(exchange) { it.answerWith(outcome) }
                }
            }
            createContext("/tabs/chat/hold") { exchange -> answer(exchange) { inTheChat(exchange) { it.hold(query(exchange, "hold") != "false") } } }
            createContext("/tabs/chat/type") { exchange ->
                answer(exchange) {
                    val text = request<TextRequest>(exchange).text
                    inTheChat(exchange) { it.type(text) }
                }
            }
            createContext("/tabs/chat/paste-image") { exchange -> answer(exchange) { inTheChat(exchange) { it.pasteAnImage() } } }
            createContext("/tabs/chat/paste") { exchange ->
                answer(exchange) {
                    val text = request<TextRequest>(exchange).text
                    inTheChat(exchange) { it.paste(text) }
                }
            }
            createContext("/tabs/chat/press") { exchange -> answer(exchange) { inTheChat(exchange) { it.press(query(exchange, "key")!!) } } }
            createContext("/tabs/chat/switch") { exchange -> answer(exchange) { inTheChat(exchange) { it.switch() } } }
            createContext("/tabs/chat/click-tab") { exchange -> answer(exchange) { inTheChat(exchange) { it.clickTheTabOf(query(exchange, "card")!!) } } }
            createContext("/tabs/chat/shortcut") { exchange ->
                answer(exchange) { inTheChat(exchange) { it.theActionTheShortcutRuns(query(exchange, "keystroke")!!, query(exchange, "in")!!) } }
            }
            createContext("/tabs/chat/fold") { exchange ->
                answer(exchange) { inTheChat(exchange) { it.openTheFold(query(exchange, "fold")!!.toInt(), query(exchange, "open") != "false") } }
            }
            createContext("/tabs/chat/scroll") { exchange -> answer(exchange) { inTheChat(exchange) { it.scrollTheTranscriptTo(query(exchange, "item")!!) } } }
            createContext("/chats/behind") { exchange ->
                answer(exchange) {
                    StandInBehindTheChatInTheSandbox.standInsBehindTheChats = when (val by = query(exchange, "by")) {
                        "standIn" -> true
                        "hub" -> false
                        else -> error("Nothing of the name $by goes behind the chats: standIn or hub.")
                    }
                    mapOf("standInsBehindTheChats" to StandInBehindTheChatInTheSandbox.standInsBehindTheChats)
                }
            }
            createContext("/tabs/chat") { exchange -> answer(exchange) { inTheChat(exchange) { it.describe() } } }
            start()
        }

    override fun dispose() {
        server?.stop(0)
        (server?.executor as ExecutorService?)?.shutdown()
    }

    private fun state(): Map<String, Any?> = mapOf(
        "startedAt" to startedAt,
        "runningClaudeCodes" to ClaudeToolingHub.getInstance().runningClaudeCodes.value.all.map { mapOf("processId" to it.process.pid(), "session" to it.session.id.value, "sessionHost" to it.sessionHost?.value) },
        "projects" to ProjectManager.getInstance().openProjects.map { project ->
            val saved = SavedClaudeSessions(project)
            mapOf(
                "name" to project.name,
                "basePath" to project.basePath,
                "view" to describe(ClaudeSessionsView.getInstance(project)),
                "keeping" to ClaudeSessionKeeper.getInstance(project).isKeeping,
                "saved" to saved.read().let { mapOf("file" to saved.location.toString(), "tabs" to it.tabs.map(::describe), "selected" to it.selected?.value) },
            )
        },
    )

    private fun describe(view: ClaudeSessionsView): Map<String, Any?> = mapOf(
        "open" to view.isOpen,
        "toolWindowVisible" to ClaudeSessionsView.toolWindowOf(view.project)?.isVisible,
        "tabs" to view.tabs.map { tab -> describe(tab) },
        "selectedTab" to view.selectedTab?.let(view.tabs::indexOf),
    )

    /** [tab]: its shell is its session host's, or its terminal's process, for a tab without a host. */
    private fun describe(tab: ClaudeSessionTab): Map<String, Any?> = mapOf(
        "name" to tab.content.displayName,
        "processId" to tab.process?.pid(),
        "sessionHost" to tab.sessionHost?.id?.value,
        "shellProcessId" to (if (tab.sessionHost != null) tab.sessionHost.shell?.pid() else tab.process?.pid()),
        "claudeCode" to tab.claudeCode?.process?.let { mapOf("processId" to it.pid(), "parentProcessId" to it.parent().orElse(null)?.pid()) },
        "session" to tab.lookAtTheSession()?.let(::describe),
    )

    private fun describe(tab: SavedTab): Map<String, Any?> = mapOf("session" to tab.session?.let(::describe), "sessionHost" to tab.sessionHost?.value)

    private fun describe(session: ClaudeSession): Map<String, Any?> =
        mapOf("id" to session.id.value, "workingDirectory" to session.workingDirectory.toString(), "name" to session.name)

    private fun openTheView(project: Project): Map<String, Any?> = onTheEdt {
        ClaudeSessionsView.toolWindowOf(project)!!.contentManager
        describe(ClaudeSessionsView.getInstance(project))
    }

    /** Off the EDT, which opens the tab once its session host has started. */
    private fun startANewSession(project: Project): Map<String, Any?> {
        val view = onTheEdt { ClaudeSessionsView.openIn(project) }
        val tab = runBlocking { onTheEdt { view.startNewSession(name = null) }.await() } ?: error("No session host started: the IDE's notification says why.")
        return onTheEdt { mapOf("tab" to view.tabs.indexOf(tab)) }
    }

    private fun openTheNewSessionPopup(project: Project): Map<String, Any?> = onTheEdt {
        NewClaudeSessionPopup.open(project, button = null)
        describeTheNewSessionPopupIn(project)
    }

    /** Does [action] to the New Claude Session popup shown in [project]'s window, and answers as `GET /new-session-popup` then. */
    private fun inTheNewSessionPopup(project: Project, action: (NewClaudeSessionPopup) -> Unit): Map<String, Any?> = onTheEdt {
        action(NewClaudeSessionPopup.shownIn(project)!!)
        describeTheNewSessionPopupIn(project)
    }

    /**
     * Has the popup's field take Enter, and answers with the place of the tab it started the session in, the last, once
     * it has opened: off the EDT, which opens the tab once its session host has started.
     */
    private fun pressEnterInTheNewSessionPopup(project: Project): Map<String, Any?> {
        val view = ClaudeSessionsView.getInstance(project)
        val before = onTheEdt { view.tabs.size.also { NewClaudeSessionPopup.shownIn(project)!!.pressEnter() } }
        val deadline = System.currentTimeMillis() + 30_000
        while (onTheEdt { view.tabs.size } == before && System.currentTimeMillis() < deadline) Thread.sleep(100)
        return mapOf("tab" to before)
    }

    private fun describeTheNewSessionPopupIn(project: Project): Map<String, Any?> {
        val popup = NewClaudeSessionPopup.shownIn(project) ?: return mapOf("shown" to false)
        return mapOf("shown" to true, "name" to popup.name)
    }

    /** Off the EDT: the terminal's settings look for the default shell, if they need it, on the calling thread, and wait. */
    private fun runTheShell(project: Project, path: String): Map<String, Any?> {
        val options = TerminalProjectOptionsProvider.getInstance(project)
        val before = options.shellPath
        options.shellPath = path
        return mapOf("before" to before, "now" to options.shellPath)
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
        val view = ClaudeSessionsView.getInstance(project).tabs[tab].terminal.view
        val output = view.outputModels.active.value
        mapOf(
            "lines" to output.getText(output.screenTopOffset, output.endOffset).toString().lines(),
            "size" to view.gridSize?.let { mapOf("columns" to it.columns, "rows" to it.rows) },
        )
    }

    private fun close(project: Project, tab: Int): Map<String, Any?> = onTheEdt {
        val view = ClaudeSessionsView.getInstance(project)
        view.close(view.tabs[tab])
        mapOf("closed" to tab)
    }

    private fun closeEveryTab(project: Project): Map<String, Any?> {
        val view = ClaudeSessionsView.getInstance(project)
        val closed = onTheEdt { view.tabs.onEach(view::close).size }
        return mapOf("closed" to closed, "left" to onTheEdt { view.tabs.size })
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

    /** Does [action] in the chat of the tab the request names, on the EDT. */
    private fun inTheChat(exchange: HttpExchange, action: (TheChatObserved) -> Map<String, Any?>): Map<String, Any?> {
        val project = projectOf(exchange)
        val tab = query(exchange, "tab")!!.toInt()
        return onTheEdt { action(TheChatObserved(project, tab)) }
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

        /** Whether the channel listens in this IDE: only in the sandbox IDE, whose `idea.properties` names its port. */
        val listens: Boolean get() = System.getProperty(PORT_PROPERTY) != null

        /** The keys `/history/press` presses, by name. */
        private val KEYS = mapOf("UP" to KeyEvent.VK_UP, "DOWN" to KeyEvent.VK_DOWN, "ENTER" to KeyEvent.VK_ENTER)
    }
}
