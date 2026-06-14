package dev.magnus.csl

import com.intellij.execution.filters.Filter

/**
 * Linkifies PascalCase identifiers and dotted member-access chains (`Type.Member`) in console
 * output, by shape. Whether a token is a real symbol — and where it goes — is decided reliably on
 * click via the ReSharper MCP (see [SymbolHyperlinkInfo]).
 */
class SymbolLinkFilter : Filter {

    private val tokenPattern =
        Regex("""[A-Z][A-Za-z0-9]{2,}(?:\.[A-Za-z_][A-Za-z0-9_]*)*""")

    override fun applyFilter(line: String, entireLength: Int): Filter.Result? {
        val lineStart = entireLength - line.length
        val items = ArrayList<Filter.ResultItem>()
        for (match in tokenPattern.findAll(line)) {
            items.add(
                Filter.ResultItem(
                    lineStart + match.range.first,
                    lineStart + match.range.last + 1,
                    SymbolHyperlinkInfo(match.value),
                ),
            )
        }
        return if (items.isEmpty()) null else Filter.Result(items)
    }
}
