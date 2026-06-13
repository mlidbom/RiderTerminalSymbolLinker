package dev.magnus.csl

import com.intellij.execution.filters.ConsoleFilterProvider
import com.intellij.execution.filters.Filter
import com.intellij.openapi.project.Project

/**
 * Registered on `com.intellij.consoleFilterProvider`. The terminal collects every provider
 * on this extension point; this spike exists to confirm that a third-party one is actually
 * consulted for reworked-terminal output.
 */
class SymbolLinkFilterProvider : ConsoleFilterProvider {
    override fun getDefaultFilters(project: Project): Array<Filter> =
        arrayOf(SymbolLinkFilter())
}
