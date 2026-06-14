package dev.magnus.csl

import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity

/**
 * On solution open, make the [SymbolIndex] available from the disk cache immediately, then rebuild it
 * fresh in the background from the ReSharper MCP. The same build is re-runnable on demand via
 * [RefreshSymbolsAction]; both go through [SymbolIndexLoader].
 */
class SymbolIndexStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        SymbolIndexLoader.loadAndRefresh(project)
    }
}
