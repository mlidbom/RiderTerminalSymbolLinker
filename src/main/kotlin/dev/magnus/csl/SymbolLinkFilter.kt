package dev.magnus.csl

import com.intellij.execution.filters.Filter
import com.intellij.openapi.project.Project

/**
 * Links the names in console output that the [SymbolIndex] knows, as [SymbolReferences] carves them: a type or member,
 * by its name or the end of its qualified name, and a namespace, by its name or the end of it from two words on. Until
 * the index is loaded the filter does nothing. A click resolves the name exactly: see [SymbolHyperlinkInfo] and
 * [NamespaceHyperlinkInfo].
 *
 * Names inside a file path or URL are skipped: Rider's terminal already turns those into native links, so
 * re-linkifying a path segment (e.g. "SymbolIndex" in ".../csl/SymbolIndex.kt") would replace a working file link
 * with a symbol link. See [PathSpans].
 */
class SymbolLinkFilter(private val project: Project) : Filter {

    override fun applyFilter(line: String, entireLength: Int): Filter.Result? {
        val index = SymbolIndex.getInstance(project)
        if (!index.isReady) return null
        val lineStart = entireLength - line.length
        val items = SymbolReferences.find(line, index.declared)
            .map { reference ->
                Filter.ResultItem(
                    lineStart + reference.range.first,
                    lineStart + reference.range.last + 1,
                    when (reference) {
                        is SymbolReference.ToATypeOrMember -> SymbolHyperlinkInfo(reference.name)
                        is SymbolReference.ToANamespace -> NamespaceHyperlinkInfo(reference.name)
                    },
                )
            }
        return if (items.isEmpty()) null else Filter.Result(items)
    }
}
