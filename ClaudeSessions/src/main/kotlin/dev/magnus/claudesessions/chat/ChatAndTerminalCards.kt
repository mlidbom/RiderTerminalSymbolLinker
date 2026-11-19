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
import java.awt.CardLayout
import java.awt.KeyboardFocusManager
import javax.swing.JComponent
import javax.swing.JPanel
import javax.swing.SwingUtilities
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch

/**
 * The content of a [ClaudeSessionTab] that has a [Chat]: two cards, the chat of [claudeCode] and the tab's terminal,
 * in a `CardLayout`, one shown at a time. The [SwitchBetweenTheChatAndTheTerminalAction] shows the other.
 *
 * **What the tab shows by itself:** the chat while Claude Code runs, busy or idle, and the terminal while it doesn't,
 * and while it waits for the user in a dialog, which only its own UI draws: a permission prompt, a question, or a
 * picker a slash command opened. When that changes, the tab switches to the terminal, and back to the chat once Claude
 * Code is busy or idle again, but only if it was the tab that switched: a card the user chose stays. A slash command
 * sent from the chat shows the terminal, where it shows what it does, and stays shown, as if the user had chosen it.
 *
 * **The terminal's card is shown until Claude Code runs,** so that a tab whose terminal starts its shell only once it
 * is shown starts it, at its size. Hidden, the terminal is still laid out, at the tab's size, and follows its resizes.
 *
 * Disposing it gives the tab's content its terminal back, as before it had a chat, and disposes the chat.
 *
 * Its methods run on the EDT.
 */
class ChatAndTerminalCards(project: Project, private val content: Content, claudeCode: ClaudeCodeBehindTheChat) : Disposable {
    private val terminal: JComponent = content.component
    private val terminalsFocus: JComponent? = content.preferredFocusableComponent
    private val cards = JPanel(CardLayout())

    val chat = Chat(project, claudeCode, afterSending = { if (it.isASlashCommand) showTheTerminal() }).also { Disposer.register(this, it) }

    /** The card shown. */
    var shown: TabCard = TabCard.TERMINAL
        private set

    /** What the tab would show by itself, as Claude Code's status last said. */
    private var whatTheTabWouldShow = TabCard.TERMINAL

    /** Whether the tab itself chose the card shown, rather than the user. */
    private var theTabChoseTheCardShown = true

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.EDT)

    init {
        cards.add(chat.component, TabCard.CHAT.name)
        cards.add(terminal, TabCard.TERMINAL.name)
        show(TabCard.TERMINAL)
        content.component = cards
        scope.launch { chat.claudeCode.status.collect(::follow) }
    }

    /** Shows the other card, as the user chose it: it stays shown until what the tab would show by itself changes. */
    fun switch() {
        theTabChoseTheCardShown = false
        show(shown.other, focusIt = true)
    }

    /** Shows the terminal, as the user chose it, as after a slash command sent from the chat. */
    fun showTheTerminal() {
        theTabChoseTheCardShown = false
        show(TabCard.TERMINAL, focusIt = true)
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
        (cards.layout as CardLayout).show(cards, card.name)
        shown = card
        val focus = focusOf(card)
        content.preferredFocusableComponent = focus
        if (focusIt) IdeFocusManager.getGlobalInstance().requestFocus(focus, true)
    }

    private fun focusOf(card: TabCard): JComponent = when (card) {
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
