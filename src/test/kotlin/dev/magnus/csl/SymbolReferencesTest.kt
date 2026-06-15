package dev.magnus.csl

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * How [SymbolReferences.find] carves links out of a line: lone known short names, and — the point of this
 * suite — a dotted `Type.Member` run linked as one combined symbol when that pair exists, falling back to
 * per-segment linking when it doesn't. The gates here are plain sets, so these tests isolate the carving
 * logic from any live index.
 */
class SymbolReferencesTest {

    private val knownShort = setOf("ApplicationWindow", "NativeWindow", "ForceToFront", "Logger", "Info", "Id")
    private val knownCombined = setOf("ApplicationWindow.ForceToFront", "NativeWindow.ForceToFront")

    private fun find(line: String, excluded: IntRange? = null): List<SymbolReferences.Ref> =
        SymbolReferences.find(
            line,
            isExcluded = { span -> excluded != null && span.first <= excluded.last && excluded.first <= span.last },
            isKnown = { it in knownShort },
            isCombined = { it in knownCombined },
        )

    @Test
    fun `a dotted access whose pair exists links as one combined symbol`() {
        val refs = find("call ApplicationWindow.ForceToFront() now")
        assertEquals(1, refs.size)
        assertEquals("ApplicationWindow.ForceToFront", refs[0].name)
        assertEquals(5..34, refs[0].range) // spans the whole Type.Member, not two adjacent tokens
        assertEquals("ApplicationWindow.ForceToFront", "call ApplicationWindow.ForceToFront() now".substring(5, 35))
    }

    @Test
    fun `a dotted access whose pair does not exist links each known segment individually`() {
        // Logger and Info are both known short names, but Logger.Info is not a known member-of-type pair.
        val refs = find("Logger.Info")
        assertEquals(listOf("Logger", "Info"), refs.map { it.name })
        assertEquals(0..5, refs[0].range)
        assertEquals(7..10, refs[1].range)
    }

    @Test
    fun `a fully qualified access links only its real member-access tail`() {
        // The namespace prefixes are not symbols, so only the ApplicationWindow.ForceToFront pair links.
        val line = "Foo.Bar.ApplicationWindow.ForceToFront"
        val refs = find(line)
        assertEquals(1, refs.size)
        assertEquals("ApplicationWindow.ForceToFront", refs[0].name)
        assertEquals("ApplicationWindow.ForceToFront", line.substring(refs[0].range.first, refs[0].range.last + 1))
    }

    @Test
    fun `a known type whose member is unknown links the type alone`() {
        val refs = find("ApplicationWindow.Unknown")
        assertEquals(listOf("ApplicationWindow"), refs.map { it.name })
        assertEquals(0..16, refs[0].range)
    }

    @Test
    fun `a lone known short name links`() {
        val refs = find("see ApplicationWindow here")
        assertEquals(1, refs.size)
        assertEquals("ApplicationWindow", refs[0].name)
        assertEquals(4..20, refs[0].range)
    }

    @Test
    fun `a short identifier under the noise-guard length does not link on its own`() {
        // "Id" is a known short name but only two chars, so it never links as a lone token.
        assertTrue(find("the Id field").isEmpty())
    }

    @Test
    fun `unknown identifiers produce nothing`() {
        assertTrue(find("just some prose here").isEmpty())
    }

    @Test
    fun `an excluded span (a path or URL Rider already linkifies) is skipped`() {
        // Pretend the whole dotted run sits inside a file path the caller marked excluded.
        assertTrue(find("ApplicationWindow.ForceToFront", excluded = 0..29).isEmpty())
    }
}
