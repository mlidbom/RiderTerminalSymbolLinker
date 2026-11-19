package dev.magnus.claudesessions

import com.intellij.openapi.actionSystem.ActionManager
import com.intellij.openapi.project.DumbAware
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowFactory
import dev.magnus.claudesessions.chat.SwitchBetweenTheChatAndTheTerminalAction
import dev.magnus.claudesessions.history.ResumeClaudeSessionAction
import dev.magnus.claudesessions.newsession.NewClaudeSessionAction
import dev.magnus.claudesessions.saving.ClaudeSessionsStartup

/**
 * Gives the Claude Sessions tool window its content as the IDE first shows it, or as [ClaudeSessionsStartup] has it
 * opened: the [ClaudeSessionsView], with the [NewClaudeSessionAction], the [ResumeClaudeSessionAction] and, for a tab
 * with a chat, the [SwitchBetweenTheChatAndTheTerminalAction] in its title bar.
 */
class ClaudeSessionsToolWindowFactory : ToolWindowFactory, DumbAware {
    override fun createToolWindowContent(project: Project, toolWindow: ToolWindow) {
        toolWindow.setToHideOnEmptyContent(false)
        toolWindow.setTitleActions(listOf(NewClaudeSessionAction.ID, ResumeClaudeSessionAction.ID, SwitchBetweenTheChatAndTheTerminalAction.ID).map(ActionManager.getInstance()::getAction))
        ClaudeSessionsView.getInstance(project).open(toolWindow)
    }
}
