package dev.magnus.csl

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project

/**
 * Project-scoped set of all C# symbol short names in the solution, built once at startup from the
 * ReSharper MCP (see [SymbolIndexStartup]). Used as the underline gate: a token is only linkified
 * if it names a real symbol. Until [isReady] (still loading, or MCP down) the filter underlines
 * nothing — we don't touch the terminal before the index exists.
 */
@Service(Service.Level.PROJECT)
class SymbolIndex {
    @Volatile
    private var names: Set<String> = emptySet()

    @Volatile
    var isReady: Boolean = false
        private set

    fun contains(name: String): Boolean = names.contains(name)

    fun set(newNames: Set<String>) {
        names = newNames
        isReady = true
    }

    companion object {
        fun getInstance(project: Project): SymbolIndex = project.service()
    }
}
