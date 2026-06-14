package dev.magnus.csl

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent

/**
 * Manual "Refresh C# Symbols" action (terminal right-click menu). Rebuilds the [SymbolIndex] so
 * symbols added or renamed since solution open start underlining in subsequent terminal output.
 */
class RefreshSymbolsAction : AnAction() {
    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        SymbolIndexLoader.refresh(project, notifyWhenDone = true)
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabled = e.project != null
    }

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT
}
