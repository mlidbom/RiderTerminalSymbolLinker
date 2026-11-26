package dev.magnus.csl

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project

/**
 * Project-scoped authority on what links in terminal output: the solution's [DeclaredNames], built at startup from the
 * .NET Analysis&Refactoring MCP (see [SymbolIndexStartup]), which [SymbolReferences] carves a line's links by. Until
 * [isReady] (still loading, or the MCP down) the filter underlines nothing: the terminal isn't touched before the index
 * exists.
 */
@Service(Service.Level.PROJECT)
class SymbolIndex {
    @Volatile
    var declared: DeclaredNames = DeclaredNames.NONE
        private set

    @Volatile
    var isReady: Boolean = false
        private set

    fun set(declared: DeclaredNames) {
        this.declared = declared
        isReady = true
    }

    companion object {
        fun getInstance(project: Project): SymbolIndex = project.service()
    }
}
