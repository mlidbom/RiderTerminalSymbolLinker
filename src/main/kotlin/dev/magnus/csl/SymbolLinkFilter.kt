package dev.magnus.csl

import com.intellij.execution.filters.Filter
import com.intellij.openapi.project.Project

/**
 * Linkifies identifier-shaped tokens in console output, but only those that name a real C# symbol in
 * the [SymbolIndex] — the index, not the shape, is the authority (see [SymbolReferences]). A lone token
 * links if it's a known short name; a dotted `Type.Member` access links as one combined symbol if that
 * pair exists. Until the index is loaded the filter does nothing. Resolution on click is exact (see
 * [SymbolHyperlinkInfo]).
 *
 * Tokens that fall inside a file path or URL are skipped: Rider's terminal already turns those into
 * native links, so re-linkifying a path segment (e.g. "SymbolIndex" in ".../csl/SymbolIndex.kt") would
 * replace a working file link with a symbol link. See [PathSpans].
 */
class SymbolLinkFilter(private val project: Project) : Filter {

    override fun applyFilter(line: String, entireLength: Int): Filter.Result? {
        val index = SymbolIndex.getInstance(project)
        if (!index.isReady) return null
        val lineStart = entireLength - line.length
        val pathRanges = PathSpans.find(line)
        val items = SymbolReferences.find(
            line,
            isExcluded = { span -> pathRanges.any { span overlaps it } },
            isKnown = { index.contains(it) },
            isCombined = { index.containsCombined(it) },
        ).map { ref ->
            Filter.ResultItem(
                lineStart + ref.range.first,
                lineStart + ref.range.last + 1,
                SymbolHyperlinkInfo(ref.name),
            )
        }
        return if (items.isEmpty()) null else Filter.Result(items)
    }

    /** Inclusive-range intersection: do these two ranges share at least one offset? */
    private infix fun IntRange.overlaps(other: IntRange): Boolean =
        first <= other.last && other.first <= last
}
