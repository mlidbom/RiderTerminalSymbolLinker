package dev.magnus.csl

import com.intellij.execution.filters.Filter
import com.intellij.openapi.project.Project

/**
 * Linkifies identifier-shaped tokens in console output, but only those that name a real C# symbol in
 * the [SymbolIndex]. The index is the sole authority on what links — we never gate by identifier shape
 * (no PascalCase rule), so underscore-prefixed fields, ALL_CAPS constants and camelCase members all
 * resolve too. Until the index is loaded the filter does nothing. Resolution on click is exact (see
 * [SymbolHyperlinkInfo]).
 */
class SymbolLinkFilter(private val project: Project) : Filter {

    // Any C# identifier (3+ chars as a noise guard); the SymbolIndex, not the shape, decides what links.
    private val identifierPattern = Regex("""[A-Za-z_][A-Za-z0-9_]{2,}""")

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
