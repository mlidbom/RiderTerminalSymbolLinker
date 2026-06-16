package dev.magnus.csl

/**
 * Finds file references in a line of console output. A reference is a maximal run of path characters
 * whose **whole** path is a real trailing path of a solution file — the [pathExists] gate decides, by
 * matching every segment, not just the file name (so `nonexisting/Vantage/Shell/AppShell.cs` is rejected
 * while `Vantage/Shell/AppShell.cs` links). There is no extension or shape heuristic.
 *
 * This links far more than Rider's native terminal does: bare names (`App.axaml.cs`), partial paths
 * (`Shell\AppShell.cs`), absolute paths, either slash direction, and references touching surrounding
 * punctuation (`Update(C:\…\AppShell.cs)`). The span boundary is what separates a usable prefix from the
 * path: a non-path character (`%`, `>`, `(`, whitespace, …) ends the run, so `#Vantage/…` links its
 * `Vantage/…` part — but a path character glued on (`nonexistingVantage/…`) is part of the path and, not
 * existing, kills the whole match. `#` ends the run too, except a `#L<line>` GitHub anchor directly after a
 * path (`Desktop.cs#L136-138`) is kept, so Rider's *add file to chat* references link.
 *
 * A trailing navigation suffix is parsed off and kept inside the link span, in either form: the colon form
 * `:line[-endLine][:col]` (Rider/compiler output) or the GitHub anchor `#Lline[-[L]endLine]` (chat prompts).
 */
object FileReferences {

    /** A maximal run of path characters, plus an optional trailing `#L<line>[-[L]endLine]` GitHub anchor
     *  (`#` is otherwise a span boundary). Stops at whitespace, brackets, quotes and commas — so a
     *  reference glued to `(`/`)`/`"` is still isolated cleanly. */
    private val pathSpan = Regex("""[\w.\-+~/\\:]+(?:#L\d+(?:-L?\d+)?)?""")

    /** A URL scheme prefix. URLs are left for Rider's native web-link handling, not treated as files. */
    private val urlScheme = Regex("""^[A-Za-z][A-Za-z0-9+.\-]*://""")

    /** Colon navigation suffix: `:line`, `:line-endLine`, `:line:col` or `:line-endLine:col` on a span
     *  (drive colons, which sit earlier in the span, are untouched). Groups: 1=line, 2=endLine, 3=col. */
    private val colonSuffix = Regex(""":(\d+)(?:-(\d+))?(?::(\d+))?$""")

    /** GitHub line-anchor suffix: `#Lline` or `#Lline-[L]endLine` (e.g. `Desktop.cs#L136-138`), the form
     *  Rider's *add file to chat* emits. No column form. Groups: 1=line, 2=endLine — same layout as
     *  [colonSuffix]'s first two, so line/endLine read uniformly from whichever matched. */
    private val anchorSuffix = Regex("""#L(\d+)(?:-L?(\d+))?$""")

    /** Trailing sentence punctuation glued to a reference (e.g. "see App.cs.") — trimmed off the span. */
    private val trailingPunctuation = setOf('.', ',', ';', ':', ')', ']', '}', '!', '?', '"', '\'')

    /**
     * One linkable file reference within a line.
     *
     * @param range      offsets within the line that the link should cover (path + line suffix)
     * @param path       the captured path including the file name, separators as written
     * @param fileName   the final path segment — what [SolutionFileNames] is keyed by
     * @param startLine  1-based line to navigate to, or null to open at the top
     * @param endLine    1-based end of a range to select (`:start-end`), or null
     * @param column     1-based column, or null
     */
    data class Ref(
        val range: IntRange,
        val path: String,
        val fileName: String,
        val startLine: Int?,
        val endLine: Int?,
        val column: Int?,
    )

    fun find(line: String, pathExists: (String) -> Boolean): List<Ref> {
        val refs = ArrayList<Ref>()
        for (match in pathSpan.findAll(line)) {
            if (urlScheme.containsMatchIn(match.value)) continue

            // Trim trailing sentence punctuation, shrinking the span so the link doesn't swallow it.
            var last = match.range.last
            while (last >= match.range.first && line[last] in trailingPunctuation) last--
            if (last < match.range.first) continue
            val span = line.substring(match.range.first, last + 1)

            // Navigation suffix: colon form (`:line[-end][:col]`) or GitHub anchor (`#Lline[-end]`). Only
            // the colon form carries a column, so it's read from `colon` alone; line/endLine come from
            // whichever matched (the two share group layout for those).
            val colon = colonSuffix.find(span)
            val suffix = colon ?: anchorSuffix.find(span)
            val path = if (suffix != null) span.substring(0, suffix.range.first) else span
            if (path.isEmpty() || !pathExists(path)) continue
            val fileName = path.substringAfterLast('/').substringAfterLast('\\')
            if (fileName.isEmpty()) continue

            refs.add(
                Ref(
                    range = match.range.first..last,
                    path = path,
                    fileName = fileName,
                    startLine = suffix?.groupValues?.get(1)?.toIntOrNull(),
                    endLine = suffix?.groupValues?.get(2)?.takeIf { it.isNotEmpty() }?.toIntOrNull(),
                    column = colon?.groupValues?.get(3)?.takeIf { it.isNotEmpty() }?.toIntOrNull(),
                ),
            )
        }
        return refs
    }
}
