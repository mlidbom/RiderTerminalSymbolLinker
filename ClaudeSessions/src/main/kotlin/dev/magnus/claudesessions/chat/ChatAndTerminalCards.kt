package dev.magnus.claudesessions.chat

import com.intellij.openapi.Disposable
import com.intellij.openapi.application.EDT
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.Disposer
import com.intellij.openapi.wm.IdeFocusManager
import com.intellij.ui.content.Content
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChat
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.chat.saving.ChatKeeper
import dev.magnus.claudesessions.chat.saving.SavedChat
import dev.magnus.claudesessions.chat.saving.SavedChats
import dev.magnus.claudesessions.sessionhosts.SessionHostId
import com.intellij.icons.AllIcons
import com.intellij.ui.components.JBTabbedPane
import com.intellij.util.ui.JBUI
import java.awt.KeyboardFocusManager
import javax.swing.JComponent
import javax.swing.SwingConstants
import javax.swing.SwingUtilities
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch

/**
 * The content of a [ClaudeSessionTab] that has a [Chat]: two cards, the chat of [claudeCode] and the tab's terminal,
 * one shown at a time, each under a tab of its own, "Chat" and "Terminal", so that both are in sight. Clicking a tab
 * shows its card, as does the [SwitchBetweenTheChatAndTheTerminalAction], Ctrl+H, the other.
 *
 * **What the tab shows by itself:** the chat while Claude Code runs, busy or idle, and the terminal while it doesn't,
 * and while it waits for the user in a dialog, which only its own UI draws: a permission prompt, a question, or a
 * picker a slash command opened. When that changes, the tab switches to the terminal, and back to the chat once Claude
 * Code is busy or idle again, but only if it was the tab that switched: a card the user chose stays. A slash command
 * sent from the chat shows the terminal, where it shows what it does, and stays shown, as if the user had chosen it; so
 * does what Claude Code's own UI shows of a message it didn't get.
 *
 * **The terminal's card is shown until Claude Code runs,** so that a tab whose terminal starts its shell only once it
 * is shown starts it, at its size. Hidden, the terminal is still laid out, at the tab's size, and follows its resizes.
 *
 * **What it holds is saved as it changes,** by a [ChatKeeper], as the chat of the tab whose session runs in [savedAs]:
 * the chat's box and the messages waiting in it, where its transcript is, and the card the user chose. Given what was
 * saved, [asItWas], it comes back as it was, as after the IDE restarted.
 *
 * Disposing it gives the tab's content its terminal back, as before it had a chat, and disposes the chat.
 *
 * Its methods run on the EDT.
 */
class ChatAndTerminalCards(
    project: Project,
    private val content: Content,
    claudeCode: ClaudeCodeBehindTheChat,
    savedChats: SavedChats,
    savedAs: SessionHostId,
    asItWas: SavedChat?,
) : Disposable {
    private val terminal: JComponent = content.component
    private val terminalsFocus: JComponent? = content.preferredFocusableComponent

    /**
     * The cards, each under its tab. Its tabs take no focus when clicked: the card shown takes it, as when the user
     * switches by the shortcut.
     */
    private val cards = JBTabbedPane(SwingConstants.TOP).apply {
        isFocusable = false
        tabComponentInsets = JBUI.emptyInsets()
    }

    /** The component that holds the cards, under their tabs: it's the tab's content. */
    val component: JComponent get() = cards

    /** Whether the cards are being shown by the code, not by the user clicking a tab. */
    private var showing = false

    private val keeper = ChatKeeper(savedChats, savedAs, chatAsItIs = { asItIs }).also { Disposer.register(this, it) }

    val chat = Chat(project, claudeCode, showTheTerminal = ::showTheTerminal, whenItChanges = keeper::changed).also { Disposer.register(this, it) }

    /** The card shown. */
    var shown: TabCard = TabCard.TERMINAL
        private set

    /** The card the user chose to show, or null when the tab chose the card shown by itself. */
    val cardTheUserChose: TabCard? get() = shown.takeUnless { theTabChoseTheCardShown }

    /** What the tab would show by itself, as Claude Code's status last said. */
    private var whatTheTabWouldShow = TabCard.TERMINAL

    /** Whether the tab itself chose the card shown, rather than the user. */
    private var theTabChoseTheCardShown = true

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.EDT)

    init {
        cards.addTab("Chat", AllIcons.Toolwindows.ToolWindowMessages, chat.component)
        cards.addTab("Terminal", AllIcons.Nodes.Console, terminal)
        show(TabCard.TERMINAL)
        cards.addChangeListener { if (!showing) theUserChose(TabCard.entries[cards.selectedIndex]) }
        content.component = cards
        asItWas?.let(::bringBack)
        scope.launch { chat.claudeCode.status.collect(::follow) }
    }

    /** The titles of the cards' tabs, in their order. For the development observation channel. */
    val tabTitles: List<String> get() = (0 until cards.tabCount).map(cards::getTitleAt)

    /** What the tab's chat holds, as it's saved. */
    private val asItIs: SavedChat
        get() = SavedChat(chat.messageBox.draft, chat.messagesNotSent, chat.messagesMaybeSent, chat.transcript.position, cardTheUserChose)

    /**
     * Brings the chat back as [asItWas], and the card the user chose, if they chose one, and saves it as this tab's: it
     * may have been saved as the chat of another session host, the one the tab's session ran in before a reboot.
     */
    private fun bringBack(asItWas: SavedChat) {
        chat.bringBack(asItWas)
        asItWas.cardTheUserChose?.let { card ->
            theTabChoseTheCardShown = false
            show(card)
        }
        keeper.changed()
    }

    /** Saves the chat as it is, at once, as the tab is detached from its session, which runs on. */
    fun saveNow() = keeper.saveNow()

    /** Forgets the saved chat, as the user closed the tab, which ended its session. */
    fun forgetTheSavedChat() = keeper.forget()

    /** Shows the other card, as the user chose it: it stays shown until what the tab would show by itself changes. */
    fun switch() = theUserChose(shown.other)

    /**
     * Shows the terminal, as the user chose it, as after a slash command sent from the chat, or a message Claude Code
     * didn't get for what its own UI shows.
     */
    fun showTheTerminal() = theUserChose(TabCard.TERMINAL)

    /** Shows [card], with the focus in it, as the user chose it, by its tab or the shortcut, or as if they had. */
    private fun theUserChose(card: TabCard) {
        theTabChoseTheCardShown = false
        show(card, focusIt = true)
    }

    private fun follow(status: ClaudeCodeStatus) {
        val would = if (status == ClaudeCodeStatus.IDLE || status == ClaudeCodeStatus.BUSY) TabCard.CHAT else TabCard.TERMINAL
        if (would == whatTheTabWouldShow) return
        whatTheTabWouldShow = would
        if (would == shown) return
        if (would == TabCard.CHAT && !theTabChoseTheCardShown) return
        theTabChoseTheCardShown = true
        show(would, focusIt = hasTheFocus())
    }

    private fun show(card: TabCard, focusIt: Boolean = false) {
        showing = true
        try {
            cards.selectedIndex = card.ordinal
        } finally {
            showing = false
        }
        shown = card
        val focus = focusOf(card)
        content.preferredFocusableComponent = focus
        if (focusIt) IdeFocusManager.getGlobalInstance().requestFocus(focus, true)
        keeper.changed()
    }

    /** Selects [card]'s tab, as the user clicking it does. For the development observation channel. */
    fun clickTheTabOf(card: TabCard) {
        cards.selectedIndex = card.ordinal
    }

    /** The component that takes the focus when [card] is shown: the chat's input box, or the terminal's. */
    fun focusOf(card: TabCard): JComponent = when (card) {
        TabCard.CHAT -> chat.messageBox.editor.contentComponent
        TabCard.TERMINAL -> terminalsFocus ?: terminal
    }

    private fun hasTheFocus(): Boolean =
        KeyboardFocusManager.getCurrentKeyboardFocusManager().focusOwner?.let { SwingUtilities.isDescendingFrom(it, cards) } == true

    override fun dispose() {
        scope.cancel()
        cards.remove(terminal)
        content.component = terminal
        content.preferredFocusableComponent = terminalsFocus
    }
}
