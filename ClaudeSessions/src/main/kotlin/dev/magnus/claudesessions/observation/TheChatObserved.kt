package dev.magnus.claudesessions.observation

import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.ui.ImageComposeScene
import androidx.compose.ui.unit.Density
import com.google.gson.JsonObject
import com.intellij.ui.scale.JBUIScale
import org.jetbrains.jewel.bridge.theme.SwingBridgeTheme
import org.jetbrains.skia.EncodedImageFormat
import com.intellij.ide.DataManager
import com.intellij.openapi.actionSystem.ActionManager
import com.intellij.openapi.actionSystem.ActionPlaces
import com.intellij.openapi.actionSystem.ActionPromoter
import com.intellij.openapi.actionSystem.ActionUiKind
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.KeyboardShortcut
import com.intellij.openapi.actionSystem.ex.ActionUtil
import com.intellij.openapi.command.WriteCommandAction
import com.intellij.openapi.editor.EditorModificationUtil
import com.intellij.openapi.keymap.KeymapManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.WindowManager
import com.intellij.util.ui.ImageUtil
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.chat.ChatAndTerminalCards
import dev.magnus.claudesessions.chat.SwitchBetweenTheChatAndTheTerminalAction
import dev.magnus.claudesessions.chat.TabCard
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.PastedImage
import dev.magnus.claudesessions.chat.claudecode.PastedText
import dev.magnus.claudesessions.chat.claudecode.TypedText
import dev.magnus.claudesessions.chat.claudecode.TypingOutcome
import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Clearing
import dev.magnus.claudesessions.chat.claudecode.conversation.Compaction
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.chat.claudecode.conversation.ConversationItem
import dev.magnus.claudesessions.chat.claudecode.conversation.Interruption
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import dev.magnus.claudesessions.chat.claudecode.conversation.Thinking
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolInputField
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolResult
import dev.magnus.claudesessions.chat.transcript.PromptFromElsewhereCaption
import dev.magnus.claudesessions.chat.transcript.PromptFromElsewhereLine
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallBody
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallHeader
import dev.magnus.claudesessions.chat.transcript.toolcalls.ToolCallStatus
import dev.magnus.claudesessions.chat.transcript.TranscriptPane
import dev.magnus.claudesessions.chat.transcript.TranscriptRow
import dev.magnus.claudesessions.chat.transcript.TranscriptView
import java.awt.Color
import java.awt.Component
import java.awt.Image
import java.awt.datatransfer.DataFlavor
import java.awt.datatransfer.StringSelection
import java.awt.datatransfer.Transferable
import java.awt.event.InputEvent
import java.awt.event.KeyEvent
import java.awt.image.BufferedImage
import java.nio.file.Files
import javax.swing.JComponent
import javax.swing.KeyStroke

/**
 * What the [ObservationChannel] sees and does in a tab's chat, for the specifications: the chat's cards, its
 * conversation, its status line and its input box, and the tab's [StandInBehindTheChat], which they drive in place of
 * a real Claude Code behind it.
 *
 * Its methods run on the EDT.
 */
class TheChatObserved(private val project: Project, private val tab: Int) {
    private val sessionTab: ClaudeSessionTab get() = ClaudeSessionsView.getInstance(project).tabs[tab]

    private val cards: ChatAndTerminalCards get() = sessionTab.cards ?: error("Tab $tab has no chat")

    private val standIn: StandInBehindTheChat get() = cards.chat.claudeCode as StandInBehindTheChat

    /**
     * The tab's chat: the card shown, or null when the tab has no chat, and the card the user chose, or null; the
     * transcript's rows, as [describe] tells each, the texts the links cover, the id of the item at the top of the pane,
     * and its find bar; the status line, and why Claude Code didn't get what the chat last gave it, or null; the text in
     * the input box, its parts, each with its kind, its caret, how many lines tall it is, and how many it has room for;
     * and the titles of the cards' tabs.
     */
    fun describe(): Map<String, Any?> {
        val cards = sessionTab.cards ?: return mapOf("card" to null)
        val transcript = cards.chat.transcript
        val draft = cards.chat.messageBox.draft
        return mapOf(
            "card" to cards.shown.name,
            "cardTheUserChose" to cards.cardTheUserChose?.name,
            "itemAtTheTop" to transcript.itemAtTheTop,
            "messageBoxParts" to describe(draft.message)["parts"],
            "caret" to draft.caret,
            "rows" to transcript.rows.map { describe(it, transcript) },
            "find" to describeTheFindOf(transcript),
            "links" to transcript.linkedTexts,
            "statusLine" to cards.chat.statusLine.text,
            "whyNotTyped" to cards.chat.statusLine.whyNotTyped,
            "messageBox" to cards.chat.messageBox.text,
            "messageBoxLines" to cards.chat.messageBox.linesItShows,
            "messageBoxRoom" to cards.chat.messageBox.linesItHasRoomFor,
            "tabs" to cards.tabTitles,
        )
    }

    /** What Claude Code got from the chat, through the stand-in: its messages, each part with its kind, and the keys it pressed. */
    fun sent(): Map<String, Any?> = mapOf(
        "messages" to standIn.sent.map(::describe),
        "interruptions" to standIn.interruptions.get(),
        "permissionModeChanges" to standIn.permissionModeChanges.get(),
    )

    /** Has the stand-in give the chat the conversation [conversation] describes, as [conversationFrom] reads it. */
    fun giveTheConversation(conversation: JsonObject): Map<String, Any?> {
        standIn.conversation.value = conversationFrom(conversation)
        return describe()
    }

    /** Has the stand-in say Claude Code's status is [status], such as `IDLE`. */
    fun sayTheStatus(status: String): Map<String, Any?> {
        standIn.status.value = ClaudeCodeStatus.valueOf(status)
        return describe()
    }

    /**
     * Has what comes of what the chat gives the stand-in be what [outcome] says: `{"outcome": "typed"}`,
     * `{"outcome": "nothingTyped", "why": …, "theTerminalShowsWhy": …}` or `{"outcome": "leftUnsentInTheTerminal", "why": …}`.
     */
    fun answerWith(outcome: JsonObject): Map<String, Any?> {
        standIn.outcome = when (val kind = outcome.get("outcome").asString) {
            "typed" -> TypingOutcome.Typed
            "nothingTyped" -> TypingOutcome.NothingTyped(outcome.get("why").asString, outcome.get("theTerminalShowsWhy").asBoolean)
            "leftUnsentInTheTerminal" -> TypingOutcome.LeftUnsentInTheTerminal(outcome.get("why").asString)
            else -> error("No outcome of the kind $kind")
        }
        return describe()
    }

    /** Has the stand-in hold what the chat gives it, when [hold], or let it go. */
    fun hold(hold: Boolean): Map<String, Any?> {
        standIn.hold(hold)
        return describe()
    }

    /** Types [text] into the input box at its caret, as from the keyboard. */
    fun type(text: String): Map<String, Any?> {
        val editor = cards.chat.messageBox.editor
        WriteCommandAction.runWriteCommandAction(project) { EditorModificationUtil.insertStringAtCaret(editor, text) }
        return describe()
    }

    /**
     * Pastes [text] into the input box, as the IDE's paste does from the clipboard: not through the system clipboard,
     * which is Magnus's too.
     */
    fun paste(text: String): Map<String, Any?> {
        cards.chat.messageBox.paste(StringSelection(text))
        return describe()
    }

    /** Pastes an image, a small red square, into the input box, as the IDE's paste does with an image on the clipboard. */
    fun pasteAnImage(): Map<String, Any?> {
        val image = ImageUtil.createImage(8, 8, BufferedImage.TYPE_INT_RGB).apply { createGraphics().run { color = Color.RED; fillRect(0, 0, 8, 8); dispose() } }
        cards.chat.messageBox.paste(ImageOnTheClipboard(image))
        return describe()
    }

    /**
     * Presses [key], `ENTER`, `CTRL_ENTER`, `SHIFT_ENTER`, `ESCAPE` or `SHIFT_TAB`, in the input box: runs the action the
     * IDE would run for it there, as [theActionTheIdeRunsFor] chooses it: the box's own, or else the editor's, as Enter's.
     */
    fun press(key: String): Map<String, Any?> {
        val component = cards.chat.messageBox.editor.contentComponent
        val action = theActionTheIdeRunsFor(KEYS.getValue(key), component) ?: error("The IDE runs nothing for $key in the input box")
        ActionManager.getInstance().tryToExecute(action, null, component, PLACE, true)
        return describe()
    }

    /** Switches between the chat and the terminal, as the action does from Find Action. */
    fun switch(): Map<String, Any?> {
        val view = ClaudeSessionsView.getInstance(project)
        view.show(sessionTab)
        // From the tab's content, which shows whichever card is shown: the IDE runs no action from a hidden component.
        ActionManager.getInstance().tryToExecute(ActionManager.getInstance().getAction(SwitchBetweenTheChatAndTheTerminalAction.ID), null, sessionTab.content.component, PLACE, true)
        return describe()
    }

    /** Shows [card], `CHAT` or `TERMINAL`, as clicking its tab does. */
    fun clickTheTabOf(card: String): Map<String, Any?> {
        cards.clickTheTabOf(TabCard.valueOf(card))
        return describe()
    }

    /**
     * The id of the action the IDE would run for [keystroke], such as `ctrl H`, pressed in the chat's input box (`chat`),
     * in the terminal (`terminal`), or elsewhere in the IDE's window (`elsewhere`), or null when none: see
     * [theActionTheIdeRunsFor]. Not the terminal's own handling of keys, which lets through only the actions its
     * `allowedActionsProvider` extensions list.
     */
    fun theActionTheShortcutRuns(keystroke: String, pressedIn: String): Map<String, Any?> {
        val component = when (pressedIn) {
            "chat" -> cards.focusOf(TabCard.CHAT)
            "transcript" -> cards.chat.transcript.conversation
            "terminal" -> cards.focusOf(TabCard.TERMINAL)
            "elsewhere" -> WindowManager.getInstance().getFrame(project)!!.rootPane
            else -> error("No place $pressedIn to press a key in")
        }
        return mapOf("action" to theActionTheIdeRunsFor(KeyStroke.getKeyStroke(keystroke)!!, component)?.let(ActionManager.getInstance()::getId))
    }

    /**
     * The action the IDE would run for [key] pressed in [component], as its key dispatcher chooses, which reads the real
     * focus, which a specification can't count on in the sandbox IDE: of the actions bound to the key, by [component] or
     * a component it's in, and then by the active keymap, the first enabled for a key pressed there, once every action
     * promoter has put them in its order. Null when none is.
     */
    private fun theActionTheIdeRunsFor(key: KeyStroke, component: JComponent): AnAction? {
        val local = generateSequence<Component>(component) { it.parent }.filterIsInstance<JComponent>()
            .flatMap { ActionUtil.getActions(it) }
            .filter { action -> action.shortcutSet.shortcuts.any { it is KeyboardShortcut && it.firstKeyStroke == key && it.secondKeyStroke == null } }
        val bound = local.toList() + KeymapManager.getInstance().activeKeymap.getActionIds(key).mapNotNull(ActionManager.getInstance()::getAction)
        val context = DataManager.getInstance().getDataContext(component)
        val inTheirOrder = ActionPromoter.EP_NAME.extensionList.fold(bound) { actions, promoter -> promoter.promote(actions, context).orEmpty().let { promoted -> promoted + (actions - promoted.toSet()) } }
        val pressed = KeyEvent(component, KeyEvent.KEY_PRESSED, System.currentTimeMillis(), key.modifiers, key.keyCode, KeyEvent.CHAR_UNDEFINED)
        return inTheirOrder.firstOrNull { action ->
            val event = AnActionEvent.createEvent(context, null, ActionPlaces.KEYBOARD_SHORTCUT, ActionUiKind.NONE, pressed)
            ActionUtil.updateAction(action, event)
            event.presentation.isEnabled
        }
    }

    /** Clicks the line of the transcript's row with the key [row]: opens a prompt from elsewhere, a tool call or a run of them, or closes it. */
    fun clickTheRow(row: String): Map<String, Any?> {
        val transcript = cards.chat.transcript
        check(transcript.rows.any { it.key == row && it !is TranscriptRow.OfAnItem }) { "The transcript has no line to click with the key $row" }
        transcript.toggle(row)
        return describe()
    }

    /** Scrolls the conversation so that the row of the item [item], by its id, is at the top of the pane, as the user would. */
    fun scrollTheTranscriptTo(item: String): Map<String, Any?> {
        cards.chat.transcript.showAtTheTop(item)
        return describe()
    }

    /**
     * A row of the transcript: its `key`; its `kind`, `prompt`, `from` (a prompt from elsewhere, with its `caption`),
     * `claude`, `thinking`, `interruption`, `compaction`, `clearing`, `tool-call` or `tool-calls` (the line of a run of
     * them); and its `text`, as it reads on the screen: what Claude said once its markdown is rendered, each part on a
     * line of its own, and the line of a prompt from elsewhere, of a tool call, or of a run, with how it went. A prompt
     * from elsewhere, and a tool call, have their `body`, shown or not, and say whether they're `open`; a tool call says
     * whether it's `inARun` too; a run says whether it's `open`.
     */
    private fun describe(row: TranscriptRow, transcript: TranscriptPane): Map<String, Any?> = when (row) {
        is TranscriptRow.OfAnItem -> {
            val item = row.item
            mapOf(
                "key" to row.key,
                "kind" to when (item) {
                    is Prompt -> "prompt"
                    is AssistantText -> "claude"
                    is Thinking -> "thinking"
                    is Interruption -> "interruption"
                    is Compaction -> "compaction"
                    is Clearing -> "clearing"
                    is PromptFromElsewhere -> error("A prompt from elsewhere shows in a row of its own")
                    is ToolCall -> error("A tool call shows in a row of its own")
                },
                "text" to transcript.texts.of(item).joinToString("\n"),
            )
        }
        is TranscriptRow.OfAPromptFromElsewhere -> mapOf(
            "key" to row.key,
            "kind" to "from",
            "caption" to PromptFromElsewhereCaption.of(row.prompt.from),
            "text" to PromptFromElsewhereLine.of(row.prompt),
            "body" to transcript.texts.of(row.prompt).joinToString("\n"),
            "open" to row.isOpen,
        )
        is TranscriptRow.OfAToolCall -> mapOf(
            "key" to row.key,
            "kind" to "tool-call",
            "text" to ToolCallHeader.of(row.call) + ToolCallStatus.of(listOf(row.call)).suffix,
            "body" to ToolCallBody.of(row.call),
            "open" to row.isOpen,
            "inARun" to row.inARun,
        )
        is TranscriptRow.OfAToolCallRun -> mapOf("key" to row.key, "kind" to "tool-calls", "text" to row.run.summary, "open" to row.isOpen)
    }

    /**
     * The transcript's find bar: whether it's `shown`, the `text` in its field, what it says of how many places hold
     * it, `count`, such as `3/17`, and the place the user is at, `current`, by its `item` and the index of its text, `part`.
     */
    private fun describeTheFindOf(transcript: TranscriptPane): Map<String, Any?> = mapOf(
        "shown" to transcript.findBar.component.isVisible,
        "text" to transcript.findBar.text,
        "count" to transcript.findBar.countSaid,
        "current" to transcript.find.current?.let(transcript.find.found::getOrNull)?.let { mapOf("item" to it.part.item, "part" to it.part.index) },
    )

    /** Opens the find bar of the transcript, and has its field hold [text], as when the user types it. */
    fun find(text: String): Map<String, Any?> {
        cards.chat.transcript.openFind()
        cards.chat.transcript.findBar.type(text)
        return describe()
    }

    /** Goes to the next place the find bar found, as Enter in its field does, or to the one before when not [forward]. */
    fun findNext(forward: Boolean): Map<String, Any?> {
        val transcript = cards.chat.transcript
        if (forward) transcript.find.next() else transcript.find.previous()
        transcript.findBar.update()
        return describe()
    }

    /** Closes the transcript's find bar, as Escape in its field does. */
    fun closeTheFind(): Map<String, Any?> {
        cards.chat.transcript.closeFind()
        return describe()
    }

    /**
     * Renders the chat's transcript, scrolled as it is, into a PNG file, [PICTURE_WIDTH] by [PICTURE_HEIGHT] at the
     * screen's scale, and tells where the file is, `file`: a picture for an agent to look at. Rendered by itself, in a
     * Compose scene of its own, as the sandbox IDE's window is on a virtual desktop of its own, where Compose draws nothing.
     */
    fun picture(): Map<String, Any?> {
        val transcript = cards.chat.transcript
        val scale = JBUIScale.sysScale(transcript.component)
        val listState = LazyListState(transcript.listState.firstVisibleItemIndex, transcript.listState.firstVisibleItemScrollOffset)
        val scene = ImageComposeScene((PICTURE_WIDTH * scale).toInt(), (PICTURE_HEIGHT * scale).toInt(), Density(scale)) {
            SwingBridgeTheme { TranscriptView(transcript, listState) }
        }
        val png = try {
            scene.render()
            scene.render().encodeToData(EncodedImageFormat.PNG)!!.bytes
        } finally {
            scene.close()
        }
        val file = Files.createTempFile("claude-sessions-transcript-", ".png")
        Files.write(file, png)
        return mapOf("file" to file.toString())
    }

    private fun describe(message: Message): Map<String, Any?> = mapOf(
        "parts" to message.parts.map { part ->
            when (part) {
                is TypedText -> mapOf("kind" to "typed", "text" to part.text)
                is PastedText -> mapOf("kind" to "pasted", "text" to part.text)
                is PastedImage -> mapOf("kind" to "image", "text" to part.text, "file" to part.file.toString(), "fileExists" to Files.isRegularFile(part.file))
            }
        },
    )

    /** An image on a clipboard, with no text. */
    private class ImageOnTheClipboard(private val image: Image) : Transferable {
        override fun getTransferDataFlavors(): Array<DataFlavor> = arrayOf(DataFlavor.imageFlavor)
        override fun isDataFlavorSupported(flavor: DataFlavor): Boolean = flavor == DataFlavor.imageFlavor
        override fun getTransferData(flavor: DataFlavor): Any = image
    }

    private companion object {
        /** The place the channel runs actions from, as the IDE's action system names where an action was invoked. */
        const val PLACE = "ClaudeSessions.Observation"

        /** How wide a [picture] of the transcript is, unscaled. */
        const val PICTURE_WIDTH = 900

        /** How high a [picture] of the transcript is, unscaled. */
        const val PICTURE_HEIGHT = 1000

        val KEYS = mapOf(
            "ENTER" to KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, 0),
            "CTRL_ENTER" to KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, InputEvent.CTRL_DOWN_MASK),
            "SHIFT_ENTER" to KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, InputEvent.SHIFT_DOWN_MASK),
            "ESCAPE" to KeyStroke.getKeyStroke(KeyEvent.VK_ESCAPE, 0),
            "SHIFT_TAB" to KeyStroke.getKeyStroke(KeyEvent.VK_TAB, InputEvent.SHIFT_DOWN_MASK),
        )

        /**
         * The conversation [json] describes: `{"items": [...]}`, each item an object with its `kind` and `id`:
         * `prompt`, `assistant-text` and `thinking` with their `text`; `from-another-session`, a prompt from elsewhere, with
         * the session's `name` and its `text`, and `from-a-background-task` with its `text`; `tool-call` with its `tool`,
         * its `input` as an object of texts, and its `result`, `{"text": …, "isAnError": …}` or null; and `compaction`.
         */
        fun conversationFrom(json: JsonObject): Conversation = Conversation(json.getAsJsonArray("items").map { itemFrom(it.asJsonObject) })

        private fun itemFrom(item: JsonObject): ConversationItem {
            val id = item.get("id").asString
            return when (val kind = item.get("kind").asString) {
                "prompt" -> Prompt(id, item.get("text").asString)
                "from-another-session" -> PromptFromElsewhere(id, PromptFromElsewhere.Source.AnotherSession(item.get("name").asString), item.get("text").asString)
                "from-a-background-task" -> PromptFromElsewhere(id, PromptFromElsewhere.Source.BackgroundTask, item.get("text").asString)
                "assistant-text" -> AssistantText(id, item.get("text").asString)
                "thinking" -> Thinking(id, item.get("text").asString)
                "tool-call" -> ToolCall(
                    id,
                    item.get("tool").asString,
                    item.getAsJsonObject("input").entrySet().map { (name, value) -> ToolInputField(name, if (value.isJsonPrimitive) value.asString else value.toString()) },
                    item.get("result")?.takeUnless { it.isJsonNull }?.asJsonObject?.let { ToolResult(it.get("text").asString, it.get("isAnError").asBoolean) },
                )
                "compaction" -> Compaction(id)
                else -> error("No conversation item of the kind $kind")
            }
        }
    }
}
