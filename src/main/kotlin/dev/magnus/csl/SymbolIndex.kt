package dev.magnus.csl

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project

/**
 * The two link gates the index serves, built together from one enumeration:
 *  - [short]    — every symbol short name (types + members, single identifiers, no dots). Gates lone
 *                 tokens like `SymbolIndex`.
 *  - [combined] — every `SimpleType.Member` pair that actually exists (e.g. `ApplicationWindow.ForceToFront`).
 *                 Gates a dotted access so it links as one symbol instead of two adjacent tokens.
 *
 * The two are disjoint by construction: a [short] name is a single identifier and never contains a dot,
 * a [combined] name always does — the [SymbolIndexCache] relies on this to store both in one flat file.
 */
data class SymbolNames(val short: Set<String>, val combined: Set<String>)

/**
 * Project-scoped authority on what links in terminal output, built once at startup from the ReSharper
 * MCP (see [SymbolIndexStartup]). A lone token links only if it [contains] a real short name; a dotted
 * `Type.Member` access links as one symbol only if it [containsCombined] a real member-of-type pair.
 * Until [isReady] (still loading, or MCP down) the filter underlines nothing — we don't touch the
 * terminal before the index exists.
 */
@Service(Service.Level.PROJECT)
class SymbolIndex {
    @Volatile
    private var short: Set<String> = emptySet()

    @Volatile
    private var combined: Set<String> = emptySet()

    @Volatile
    var isReady: Boolean = false
        private set

    /** Whether [name] is a known short name (a single identifier — type or member). */
    fun contains(name: String): Boolean = short.contains(name)

    /** Whether [name] is a known `SimpleType.Member` combined identifier (a dotted access that exists). */
    fun containsCombined(name: String): Boolean = combined.contains(name)

    fun set(symbols: SymbolNames) {
        short = symbols.short
        combined = symbols.combined
        isReady = true
    }

    companion object {
        fun getInstance(project: Project): SymbolIndex = project.service()
    }
}
