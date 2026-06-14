package dev.magnus.csl

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent

/**
 * Manual "Refresh Terminal Links" action (terminal right-click menu). Rebuilds both the [SymbolIndex]
 * and the [SolutionFileNames] file index, so symbols and files added or renamed since solution open
 * start linking in subsequent terminal output.
 */
class RefreshSymbolsAction : AnAction() {
    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        SymbolIndexLoader.refresh(project, notifyWhenDone = true)
        SolutionFileIndexLoader.refresh(project, notifyWhenDone = true)
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabled = e.project != null
    }

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT
}
