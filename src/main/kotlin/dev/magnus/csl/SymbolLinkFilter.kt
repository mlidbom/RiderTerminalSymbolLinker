package dev.magnus.csl

import com.intellij.execution.filters.Filter
import com.intellij.openapi.diagnostic.Logger

/**
 * Spike heuristic only: linkify PascalCase words and dotted member-access chains.
 * Over-matches on purpose; logs every call so idea.log shows whether the terminal feeds us lines.
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
        LOG.warn("CSL-SPIKE applyFilter matches=${items.size} len=${line.length} text=${line.take(100).trim()}")
        return if (items.isEmpty()) null else Filter.Result(items)
    }

    companion object {
        private val LOG = Logger.getInstance("CSL-SPIKE")
    }
}
