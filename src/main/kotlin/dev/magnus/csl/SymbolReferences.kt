package dev.magnus.csl

/**
 * Finds the symbol references to linkify in a line of console output. The [SymbolIndex] gates — never an
 * identifier-shape rule — decide what links, so underscore-prefixed fields, ALL_CAPS constants and
 * camelCase members all resolve.
 *
 * The one shape decision here is how a *dotted* run is carved up. A run like `ApplicationWindow.ForceToFront`
 * is preferentially linked as a single combined identifier when that exact `Type.Member` pair exists
 * ([isCombined]); only the segments left over after pulling out combined pairs fall back to linking
 * individually as lone short names ([isKnown]). Scanning left to right and taking the longest combined
 * pair greedily means a fully-qualified `Foo.Bar.ApplicationWindow.ForceToFront` links its real
 * `ApplicationWindow.ForceToFront` member-access — the namespace prefixes aren't symbols, so they're
 * passed over and the combined pair is found at the end.
 *
 * Spans the caller marks as [isExcluded] (a file path or URL Rider already linkifies natively) are
 * skipped so a symbol link never overwrites a working file link.
 */
object SymbolReferences {

    /** A dotted run of identifiers, e.g. `ApplicationWindow.ForceToFront`, or a bare `SymbolIndex`. */
    private val dottedRun = Regex("""[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*""")

    /** Noise guard for a *lone* identifier; a combined identifier is gated by its existence instead. */
    private const val MIN_SINGLE_LENGTH = 3

    /** One linkable symbol reference within a line: the span to cover and the name to resolve on click. */
    data class Ref(val range: IntRange, val name: String)

    fun find(
        line: String,
        isExcluded: (IntRange) -> Boolean,
        isKnown: (String) -> Boolean,
        isCombined: (String) -> Boolean,
    ): List<Ref> {
        val refs = ArrayList<Ref>()
        for (run in dottedRun.findAll(line)) {
            val segments = segmentsOf(run)
            var i = 0
            while (i < segments.size) {
                val pair = if (i + 1 < segments.size) "${segments[i].second}.${segments[i + 1].second}" else null
                if (pair != null && isCombined(pair)) {
                    // A known pair is consumed as one unit (the segments don't fall back to linking alone).
                    val span = segments[i].first.first..segments[i + 1].first.last
                    if (!isExcluded(span)) refs.add(Ref(span, pair))
                    i += 2
                    continue
                }
                val (range, segment) = segments[i]
                if (segment.length >= MIN_SINGLE_LENGTH && isKnown(segment) && !isExcluded(range)) {
                    refs.add(Ref(range, segment))
                }
                i++
            }
        }
        return refs
    }

    /** Split a dotted run into its segments, each paired with its inclusive offset range within the line. */
    private fun segmentsOf(run: MatchResult): List<Pair<IntRange, String>> {
        val segments = ArrayList<Pair<IntRange, String>>()
        var start = run.range.first
        for (segment in run.value.split('.')) {
            segments.add((start..start + segment.length - 1) to segment)
            start += segment.length + 1 // skip the '.'
        }
        return segments
    }
}
