package dev.magnus.csl

import com.intellij.execution.filters.ConsoleFilterProvider
import com.intellij.execution.filters.Filter
import com.intellij.openapi.project.Project

class SymbolLinkFilterProvider : ConsoleFilterProvider {
    override fun getDefaultFilters(project: Project): Array<Filter> =
        //bug: in a Run console, which applies its filters as one CompositeFilter, a line with a symbol link gets none of
        //   its file links: a Filter.Result ends the filtering of its line unless its nextAction says
        //   CONTINUE_FILTERING, and neither filter's does. The terminal runs each filter by itself, so it links both.
        //   Found 2026-11-19, as Claude Sessions' chat ran them as one CompositeFilter; not checked in a Run console.
        arrayOf(SymbolLinkFilter(project), FileLinkFilter(project))
}
