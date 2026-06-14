package dev.magnus.csl

import com.intellij.execution.filters.Filter
import com.intellij.openapi.project.Project

/**
 * Linkifies file references in console output — any path token whose final segment names a real file in
 * the solution (see [FileReferences] and the [SolutionFileNames] gate). Runs alongside [SymbolLinkFilter];
 * the two don't collide because the symbol filter already excludes path-shaped spans, which is exactly
 * what this one claims. Like the symbol filter it links nothing until its index is ready.
 *
 * The links are ordinary console hyperlinks, so — unlike Rider's native terminal path detection, which
 * only reveals a link on Ctrl-hover — they are underlined and visible from the moment the line is printed.
 */
class FileLinkFilter(private val project: Project) : Filter {

    override fun applyFilter(line: String, entireLength: Int): Filter.Result? {
        val index = SolutionFileNames.getInstance(project)
        if (!index.isReady) return null
        val lineStart = entireLength - line.length
        val items = FileReferences.find(line) { index.matches(it) }.map { ref ->
            Filter.ResultItem(
                lineStart + ref.range.first,
                lineStart + ref.range.last + 1,
                FileHyperlinkInfo(ref.path, ref.fileName, ref.startLine, ref.endLine, ref.column),
            )
        }
        return if (items.isEmpty()) null else Filter.Result(items)
    }
}
