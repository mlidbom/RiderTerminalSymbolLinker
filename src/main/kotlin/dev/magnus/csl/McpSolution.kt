package dev.magnus.csl

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.project.Project

/**
 * Resolves and caches the ReSharper-MCP `solutionName` for this Rider project. With several solutions
 * open in one Rider process the MCP serves them all from one server, so every call must say which
 * solution it means — omitting it errors or silently answers from the wrong one.
 *
 * The project maps to exactly one loaded solution, identified by matching the project's own solution
 * directory against the paths from `list_solutions`. We return that solution's [SolutionInfo.routingKey]
 * (its `uniquePathSegment`, not its bare name) so calls reach this exact solution even when two loaded
 * solutions share a name — e.g. two git worktrees of the same repo, both named "Vantage". Returning the
 * bare name there routes to whichever same-named solution the server picks first, opening the wrong tree.
 *
 * The mapping never changes for a project, so it is resolved once (off the EDT — it makes an HTTP call)
 * and cached. `null` only while the MCP is unreachable, so resolution is retried until it succeeds.
 */
@Service(Service.Level.PROJECT)
class McpSolution(private val project: Project) {
    @Volatile
    private var cached: String? = null

    /** The `solutionName` for this project, or `null` if the MCP is unreachable. Never call on the EDT. */
    fun name(): String? {
        cached?.let { return it }
        return resolve()?.also { cached = it }
    }

    private fun resolve(): String? {
        val solutions = ReSharperMcp.listSolutions()?.takeIf { it.isNotEmpty() } ?: return null
        val base = project.basePath?.let(::normalize)
        if (base != null) {
            solutions.firstOrNull { normalize(it.path).substringBeforeLast('/') == base }?.let { return it.routingKey }
        }
        solutions.firstOrNull { it.name.equals(project.name, ignoreCase = true) }?.let { return it.routingKey }
        if (solutions.size == 1) return solutions.first().routingKey
        LOG.warn("CSL: no MCP solution matched project '${project.name}' (basePath=${project.basePath}); guessing by project name")
        return project.name
    }

    private fun normalize(path: String): String = path.replace('\\', '/').trimEnd('/').lowercase()

    companion object {
        private val LOG = Logger.getInstance("CSL")
        fun getInstance(project: Project): McpSolution = project.service()
    }
}
