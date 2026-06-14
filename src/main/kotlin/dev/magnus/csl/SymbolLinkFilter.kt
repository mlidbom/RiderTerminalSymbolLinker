package dev.magnus.csl

import com.intellij.execution.filters.Filter
import com.intellij.openapi.project.Project

/**
 * Linkifies PascalCase identifiers in console output, but only tokens that name a real C# symbol in
 * the [SymbolIndex]. Until the index is loaded the filter does nothing — we never underline by shape.
 * Resolution on click is exact (see [SymbolHyperlinkInfo]).
 */
class SymbolLinkFilter(private val project: Project) : Filter {

    private val identifierPattern = Regex("""[A-Z][A-Za-z0-9]{2,}""")

    override fun applyFilter(line: String, entireLength: Int): Filter.Result? {
        val index = SymbolIndex.getInstance(project)
        if (!index.isReady) return null
        val lineStart = entireLength - line.length
        val items = ArrayList<Filter.ResultItem>()
        for (match in identifierPattern.findAll(line)) {
            val name = match.value
            if (!index.contains(name)) continue
            items.add(
                Filter.ResultItem(
                    lineStart + match.range.first,
                    lineStart + match.range.last + 1,
                    SymbolHyperlinkInfo(name),
                ),
            )
        }
        return if (items.isEmpty()) null else Filter.Result(items)
    }
}
