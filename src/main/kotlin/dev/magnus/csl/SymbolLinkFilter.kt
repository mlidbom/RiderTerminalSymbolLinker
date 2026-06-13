package dev.magnus.csl

import com.intellij.execution.filters.Filter

/**
 * Spike heuristic only: linkify PascalCase words and dotted member-access chains
 * (e.g. `IVirtualDesktopNotification`, `Resolver.CurrentDesktop`). This deliberately
 * over-matches — the point is to see whether the produced hyperlinks render and click
 * in the terminal, not to be precise yet.
 */
class SymbolLinkFilter : Filter {

    private val tokenPattern =
        Regex("""[A-Z][A-Za-z0-9]{2,}(?:\.[A-Za-z_][A-Za-z0-9_]*)*""")

    override fun applyFilter(line: String, entireLength: Int): Filter.Result? {
        val lineStart = entireLength - line.length
        val items = ArrayList<Filter.ResultItem>()
        for (match in tokenPattern.findAll(line)) {
            val start = lineStart + match.range.first
            val end = lineStart + match.range.last + 1
            items.add(Filter.ResultItem(start, end, SymbolHyperlinkInfo(match.value)))
        }
        return if (items.isEmpty()) null else Filter.Result(items)
    }
}
