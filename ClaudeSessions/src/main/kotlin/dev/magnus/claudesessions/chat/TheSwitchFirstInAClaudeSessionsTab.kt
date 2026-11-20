package dev.magnus.claudesessions.chat

import com.intellij.openapi.actionSystem.ActionManager
import com.intellij.openapi.actionSystem.ActionPromoter
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.DataContext

/**
 * Puts the [SwitchBetweenTheChatAndTheTerminalAction] first among the actions a pressed shortcut is bound to, so that
 * Ctrl+H switches in a Claude Sessions tab rather than run Replace, or Type Hierarchy, which the keymaps bind it to as
 * well: the IDE runs the first of them that is enabled, and the switch is enabled for a key only in such a tab.
 * Registered on the platform's `actionPromoter` extension point.
 */
class TheSwitchFirstInAClaudeSessionsTab : ActionPromoter {
    override fun promote(actions: List<AnAction>, context: DataContext): List<AnAction> =
        listOfNotNull(ActionManager.getInstance().getAction(SwitchBetweenTheChatAndTheTerminalAction.ID).takeIf { it in actions })
}
