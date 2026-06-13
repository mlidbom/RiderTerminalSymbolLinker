package dev.magnus.csl

import com.intellij.execution.filters.ConsoleFilterProvider
import com.intellij.execution.filters.Filter
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.project.Project

/**
 * Registered on `com.intellij.consoleFilterProvider`. The terminal collects providers on this
 * extension point; this spike confirms whether a third-party one is actually consulted for
 * reworked-terminal output. Logs so we can see in idea.log whether it is even called.
 */
class SymbolLinkFilterProvider : ConsoleFilterProvider {
    override fun getDefaultFilters(project: Project): Array<Filter> {
        LOG.warn("CSL-SPIKE getDefaultFilters CALLED (project=${project.name})")
        return arrayOf(SymbolLinkFilter())
    }

    companion object {
        private val LOG = Logger.getInstance("CSL-SPIKE")
    }
}
