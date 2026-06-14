package dev.magnus.csl

import com.intellij.execution.filters.Filter
import com.intellij.openapi.project.Project

/**
 * Linkifies PascalCase identifiers in console output. Once the [SymbolIndex] is loaded, only tokens
 * that name a real C# symbol are underlined; while it is still loading (or the MCP is unavailable)
 * it falls back to underlining by shape so links keep working. Resolution on click is exact (see
 * [SymbolHyperlinkInfo]).
 */
class SymbolLinkFilter(private val project: Project) : Filter {

    private val identifierPattern = Regex("""[A-Z][A-Za-z0-9]{2,}""")

    override fun applyFilter(line: String, entireLength: Int): Filter.Result? {
        val index = SymbolIndex.getInstance(project)
        val lineStart = entireLength - line.length
        val items = ArrayList<Filter.ResultItem>()
        for (match in identifierPattern.findAll(line)) {
            val name = match.value
            if (index.isReady && !index.contains(name)) continue
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
