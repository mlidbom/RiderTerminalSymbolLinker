package dev.magnus.claudesessions.chat

import com.intellij.icons.AllIcons
import com.intellij.openapi.actionSystem.ActionPlaces
import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.PlatformCoreDataKeys
import com.intellij.openapi.project.DumbAwareAction
import com.intellij.openapi.project.Project
import dev.magnus.claudesessions.ClaudeSessionsView
import javax.swing.SwingUtilities

/**
 * Shows the other card of the Claude Sessions tab the tool window shows: the terminal while it shows the chat, and the
 * chat while it shows the terminal, as clicking the other's tab does. Its shortcut, Ctrl+H, works only with the focus
 * in that tab, in the chat and in the terminal alike (see [TheChatsActionsInTheTerminal]), where
 * [TheSwitchFirstInAClaudeSessionsTab] puts it ahead of the other actions Ctrl+H is bound to, such as Replace. Everywhere
 * else it's disabled, and they run.
 */
class SwitchBetweenTheChatAndTheTerminalAction : DumbAwareAction() {
    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.EDT

    override fun update(event: AnActionEvent) {
        val cards = event.project?.let(::cardsShownIn)?.takeUnless { event.place == ActionPlaces.KEYBOARD_SHORTCUT && !it.holdsTheFocusOf(event) }
        event.presentation.isEnabledAndVisible = cards != null
        if (cards == null) return
        event.presentation.text = if (cards.shown == TabCard.CHAT) "Show the Terminal" else "Show the Chat"
        event.presentation.icon = if (cards.shown == TabCard.CHAT) AllIcons.Nodes.Console else AllIcons.Toolwindows.ToolWindowMessages
    }

    override fun actionPerformed(event: AnActionEvent) {
        event.project?.let(::cardsShownIn)?.switch()
    }

    /** The cards of the tab [project]'s tool window shows, if it has a chat. */
    private fun cardsShownIn(project: Project): ChatAndTerminalCards? =
        ClaudeSessionsView.getInstance(project).takeIf { it.isOpen }?.selectedTab?.cards

    /** Whether the component [event] came from, the one with the focus for a key pressed, is in these cards. */
    private fun ChatAndTerminalCards.holdsTheFocusOf(event: AnActionEvent): Boolean =
        event.getData(PlatformCoreDataKeys.CONTEXT_COMPONENT)?.let { SwingUtilities.isDescendingFrom(it, component) } == true

    companion object {
        /** The action's id in plugin.xml. */
        const val ID = "ClaudeSessions.SwitchBetweenTheChatAndTheTerminal"
    }
}
