package dev.magnus.claudesessions.observation

import com.google.gson.JsonObject
import com.intellij.ide.DataManager
import com.intellij.openapi.actionSystem.ActionManager
import com.intellij.openapi.actionSystem.ActionUiKind
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.KeyboardShortcut
import com.intellij.openapi.actionSystem.ex.ActionUtil
import com.intellij.openapi.command.WriteCommandAction
import com.intellij.openapi.editor.EditorModificationUtil
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.TextRange
import com.intellij.util.ui.ImageUtil
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.chat.ChatAndTerminalCards
import dev.magnus.claudesessions.chat.SwitchBetweenTheChatAndTheTerminalAction
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.PastedImage
import dev.magnus.claudesessions.chat.claudecode.PastedText
import dev.magnus.claudesessions.chat.claudecode.TypedText
import dev.magnus.claudesessions.chat.claudecode.conversation.AssistantText
import dev.magnus.claudesessions.chat.claudecode.conversation.Compaction
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.chat.claudecode.conversation.ConversationItem
import dev.magnus.claudesessions.chat.claudecode.conversation.Prompt
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolInputField
import dev.magnus.claudesessions.chat.claudecode.conversation.ToolResult
import java.awt.Color
import java.awt.Image
import java.awt.datatransfer.DataFlavor
import java.awt.datatransfer.StringSelection
import java.awt.datatransfer.Transferable
import java.awt.event.InputEvent
import java.awt.event.KeyEvent
import java.awt.image.BufferedImage
import java.nio.file.Files
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
     * The tab's chat: the card shown, or null when the tab has no chat; the conversation's text, each fold of it with
     * its header line, its folded text and whether it is open, and the texts the links cover; the status line; the text
     * in the input box; and the button's text.
     */
    fun describe(): Map<String, Any?> {
        val cards = sessionTab.cards ?: return mapOf("card" to null)
        val transcript = cards.chat.transcript
        val document = transcript.editor.document
        return mapOf(
            "card" to cards.shown.name,
            "transcript" to transcript.text,
            "folds" to transcript.folds.map { fold ->
                val line = document.getLineNumber(fold.startOffset)
                mapOf(
                    "header" to document.getText(TextRange(document.getLineStartOffset(line), fold.startOffset)),
                    "folded" to document.getText(fold.textRange),
                    "open" to fold.isExpanded,
                )
            },
            "links" to transcript.linkedTexts,
            "statusLine" to cards.chat.statusLine.text,
            "messageBox" to cards.chat.messageBox.text,
            "button" to buttonText(),
        )
    }

    /** What the chat sent to the stand-in: its messages, each part with its kind, and the keys it pressed. */
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
     * Presses [key], `ENTER`, `SHIFT_ENTER`, `ESCAPE` or `SHIFT_TAB`, in the input box: runs the action the box binds it
     * to, if it's enabled, as the IDE does for a key pressed in it.
     */
    fun press(key: String): Map<String, Any?> {
        val keyStroke = KEYS.getValue(key)
        val component = cards.chat.messageBox.editor.contentComponent
        val action = ActionUtil.getActions(component).single { action -> action.shortcutSet.shortcuts.any { it is KeyboardShortcut && it.firstKeyStroke == keyStroke } }
        ActionManager.getInstance().tryToExecute(action, null, component, PLACE, true)
        return describe()
    }

    /** Switches between the chat and the terminal, as the button in the tool window's title bar does. */
    fun switch(): Map<String, Any?> {
        val view = ClaudeSessionsView.getInstance(project)
        view.show(sessionTab)
        ActionManager.getInstance().tryToExecute(ActionManager.getInstance().getAction(SwitchBetweenTheChatAndTheTerminalAction.ID), null, cards.chat.component, PLACE, true)
        return describe()
    }

    /** Opens the fold [fold], by its place among the folds, or closes it when not [open]. */
    fun openTheFold(fold: Int, open: Boolean): Map<String, Any?> {
        val editor = cards.chat.transcript.editor
        editor.foldingModel.runBatchFoldingOperation { cards.chat.transcript.folds[fold].isExpanded = open }
        return describe()
    }

    private fun buttonText(): String? {
        val action = ActionManager.getInstance().getAction(SwitchBetweenTheChatAndTheTerminalAction.ID)
        val event = AnActionEvent.createEvent(DataManager.getInstance().getDataContext(cards.chat.component), null, PLACE, ActionUiKind.NONE, null)
        ActionUtil.updateAction(action, event)
        return event.presentation.text
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

        val KEYS = mapOf(
            "ENTER" to KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, 0),
            "SHIFT_ENTER" to KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, InputEvent.SHIFT_DOWN_MASK),
            "ESCAPE" to KeyStroke.getKeyStroke(KeyEvent.VK_ESCAPE, 0),
            "SHIFT_TAB" to KeyStroke.getKeyStroke(KeyEvent.VK_TAB, InputEvent.SHIFT_DOWN_MASK),
        )

        /**
         * The conversation [json] describes: `{"items": [...]}`, each item an object with its `kind` and `id`:
         * `prompt` and `assistant-text` with their `text`; `tool-call` with its `tool`, its `input` as an object of
         * texts, and its `result`, `{"text": …, "isAnError": …}` or null; and `compaction`.
         */
        fun conversationFrom(json: JsonObject): Conversation = Conversation(json.getAsJsonArray("items").map { itemFrom(it.asJsonObject) })

        private fun itemFrom(item: JsonObject): ConversationItem {
            val id = item.get("id").asString
            return when (val kind = item.get("kind").asString) {
                "prompt" -> Prompt(id, item.get("text").asString)
                "assistant-text" -> AssistantText(id, item.get("text").asString)
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
