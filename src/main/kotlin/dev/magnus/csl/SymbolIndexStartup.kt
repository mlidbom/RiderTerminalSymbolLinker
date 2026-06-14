package dev.magnus.csl

import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity

/**
 * On solution open, build the [SymbolIndex] in the background (with a visible progress bar) by
 * enumerating all solution symbols from the ReSharper MCP. The same build is re-runnable on demand
 * via [RefreshSymbolsAction]; both go through [SymbolIndexLoader].
 */
class SymbolIndexStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        SymbolIndexLoader.refresh(project, notifyWhenDone = false)
    }
}
