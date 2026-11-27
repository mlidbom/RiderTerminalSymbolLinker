package dev.magnus.csl

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.jetbrains.rider.projectView.solutionFile

/**
 * The solution this Rider project has open, as the .NET Analysis&Refactoring MCP knows it: the absolute path of its
 * solution file, which every call passes as `solutionName`. The MCP matches it by its full path, so a call reaches this
 * very solution, in the backend of this Rider, whatever other solutions share its name or its folder, as two worktrees
 * of one repository do, or the twenty solutions in Compze's folder.
 *
 * A project with no solution file, such as Rider's home project, has no solution for the MCP, so the plugin makes no
 * calls for it.
 */
@Service(Service.Level.PROJECT)
class McpSolution(private val project: Project) {
    @Volatile
    private var served: String? = null

    /** Whether the project has a solution file, and so a solution the MCP can serve. */
    val exists: Boolean get() = project.solutionFile.isFile

    /**
     * The `solutionName` for this project once the MCP serves its solution, or `null` while it doesn't yet. The MCP
     * serves a solution once Rider's backend has registered it, seconds after it opens: asked for it before then, the
     * MCP would open the solution again in a Rider of its own. Never call on the EDT, as it may ask the MCP.
     */
    fun name(): String? {
        served?.let { return it }
        if (!exists) return null
        val solutionFile = project.solutionFile
        return solutionFile.path.takeIf { DotNetAnalysisAndRefactoringMcp.serves(solutionFile) }?.also { served = it }
    }

    companion object {
        fun getInstance(project: Project): McpSolution = project.service()
    }
}
