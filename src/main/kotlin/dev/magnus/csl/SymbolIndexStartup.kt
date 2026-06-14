package dev.magnus.csl

import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.openapi.progress.ProgressManager
import com.intellij.openapi.progress.Task
import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity

/**
 * On solution open, build the [SymbolIndex] in the background (with a visible progress bar) by
 * enumerating all solution symbols from the ReSharper MCP. Cheap (a handful of batched calls).
 */
class SymbolIndexStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        ProgressManager.getInstance().run(
            object : Task.Backgroundable(project, "Loading C# symbols for Claude links", true) {
                override fun run(indicator: ProgressIndicator) {
                    val names = ReSharperMcp.enumerateSymbolNames(indicator) ?: return
                    SymbolIndex.getInstance(project).set(names)
                }
            },
        )
    }
}
