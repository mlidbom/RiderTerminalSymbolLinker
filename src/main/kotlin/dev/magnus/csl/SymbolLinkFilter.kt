package dev.magnus.csl

import com.intellij.execution.filters.Filter
import com.intellij.openapi.project.Project

/**
 * Linkifies identifier-shaped tokens in console output, but only those that name a real C# symbol in
 * the [SymbolIndex]. The index is the sole authority on what links — we never gate by identifier shape
 * (no PascalCase rule), so underscore-prefixed fields, ALL_CAPS constants and camelCase members all
 * resolve too. Until the index is loaded the filter does nothing. Resolution on click is exact (see
 * [SymbolHyperlinkInfo]).
 *
 * Tokens that fall inside a file path or URL are skipped: Rider's terminal already turns those into
 * native links, so re-linkifying a path segment (e.g. "SymbolIndex" in ".../csl/SymbolIndex.kt") would
 * replace a working file link with a symbol link. See [filePathRanges].
 */
class SymbolLinkFilter(private val project: Project) : Filter {

    // Any C# identifier (3+ chars as a noise guard); the SymbolIndex, not the shape, decides what links.
    private val identifierPattern = Regex("""[A-Za-z_][A-Za-z0-9_]{2,}""")

    // A non-whitespace run containing a directory separator — a relative/absolute path or a URL.
    private val pathWithSeparator = Regex("""[\w.:~\-]*[/\\][\w.:/\\~\-]*""")

    // A bare "<name>.<ext>" file reference (no directory), optionally trailed by ":line[:col]". The
    // extension is whitelisted so qualified C# names like "Logger.Info" are not mistaken for files.
    private val fileNameWithExtension = Regex(
        """[\w.\-]+\.(?:cs|csproj|sln|slnx|kt|kts|java|xml|json|ya?ml|txt|md|props|targets|config|""" +
            """resx|razor|cshtml|js|ts|tsx|jsx|html?|css|py|go|rs|rb|sql|sh|ps1|gradle|properties|toml|ini)""" +
            """(?::\d+(?::\d+)?)?""",
    )

    override fun applyFilter(line: String, entireLength: Int): Filter.Result? {
        val index = SymbolIndex.getInstance(project)
        if (!index.isReady) return null
        val lineStart = entireLength - line.length
        val pathRanges = filePathRanges(line)
        val items = ArrayList<Filter.ResultItem>()
        for (match in identifierPattern.findAll(line)) {
            val name = match.value
            if (!index.contains(name)) continue
            if (pathRanges.any { match.range overlaps it }) continue
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

    /** Spans of [line] that Rider already linkifies as file references, which we must not overwrite. */
    private fun filePathRanges(line: String): List<IntRange> =
        (pathWithSeparator.findAll(line) + fileNameWithExtension.findAll(line)).map { it.range }.toList()

    /** Inclusive-range intersection: do these two ranges share at least one offset? */
    private infix fun IntRange.overlaps(other: IntRange): Boolean =
        first <= other.last && other.first <= last
}
