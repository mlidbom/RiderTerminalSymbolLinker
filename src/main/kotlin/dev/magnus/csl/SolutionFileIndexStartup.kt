package dev.magnus.csl

import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity

/**
 * On solution open, build the [SolutionFileNames] index so file references in terminal output become
 * clickable. Re-runnable on demand via [RefreshSymbolsAction]; both go through [SolutionFileIndexLoader].
 * Separate from [SymbolIndexStartup] so the two indexes build independently — neither blocks the other.
 */
class SolutionFileIndexStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        SolutionFileIndexLoader.loadAndRefresh(project)
    }
}
