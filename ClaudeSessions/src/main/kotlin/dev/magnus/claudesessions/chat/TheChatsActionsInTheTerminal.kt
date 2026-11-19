package dev.magnus.claudesessions.chat

import com.intellij.terminal.frontend.view.TerminalAllowedActionsProvider

/**
 * The chat's actions whose shortcuts work while the terminal has the focus: the terminal sends every other key to the
 * program it runs, Claude Code here, when the IDE's terminal settings have it override the IDE's shortcuts, as they do
 * unless changed. Registered on the terminal plugin's extension point `allowedActionsProvider`.
 */
class TheChatsActionsInTheTerminal : TerminalAllowedActionsProvider {
    override fun getActionIds(): List<String> = listOf(SwitchBetweenTheChatAndTheTerminalAction.ID)
}
