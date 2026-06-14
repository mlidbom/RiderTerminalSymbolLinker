package dev.magnus.csl

/**
 * The file-path matching rules, as a pure value over a snapshot of the solution's files: each lowercased
 * file name maps to the full disk paths that bear it. Separated from the [SolutionFileNames] project
 * service so the rules are unit-testable without the platform.
 *
 * A path matches only when its **entire** segment sequence is a trailing path of a real file — every
 * segment must line up, not just the file name. So `Shell/AppShell.cs` and a full path match, but
 * `nonexisting/Vantage/Shell/AppShell.cs` does not (no file ends with that), even though a file named
 * `AppShell.cs` exists. A bare name matches every file of that name. Matching is case-insensitive
 * (Windows file systems are) and separator-agnostic.
 */
class SolutionFileIndex(private val byName: Map<String, List<String>>) {

    /** Whether [path]'s full segment sequence is a trailing path of a real solution file. */
    fun matches(path: String): Boolean = resolve(path).isNotEmpty()

    /**
     * Solution files whose path ends with [path]'s segments, aligned segment-for-segment. A bare file
     * name returns every file of that name; a partial or full path returns only those it is a true suffix
     * of. Empty when nothing aligns — which is also how [matches] rejects a path with a bogus segment.
     */
    fun resolve(path: String): List<String> {
        val refSegments = segments(path)
        val name = refSegments.lastOrNull() ?: return emptyList()
        val candidates = byName[name] ?: return emptyList()
        if (refSegments.size == 1) return candidates // bare name: every same-named file qualifies
        return candidates.filter { isTrailingPath(refSegments, segments(it)) }
    }

    /** Do [ref]'s segments equal the last `ref.size` segments of [candidate]? */
    private fun isTrailingPath(ref: List<String>, candidate: List<String>): Boolean {
        if (ref.size > candidate.size) return false
        for (i in 1..ref.size) if (ref[ref.size - i] != candidate[candidate.size - i]) return false
        return true
    }

    /** Lowercased path segments, dropping empties and a leading drive letter (`c:`). */
    private fun segments(path: String): List<String> =
        path.replace('\\', '/').split('/').filter { it.isNotEmpty() && !it.endsWith(":") }.map { it.lowercase() }

    companion object {
        val EMPTY = SolutionFileIndex(emptyMap())
    }
}
