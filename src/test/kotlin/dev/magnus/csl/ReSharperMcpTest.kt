package dev.magnus.csl

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

/**
 * Parsing of the `list_solutions` payload, with the worktree-collision case as the point of the suite: two
 * git worktrees of one repo load two solutions that share a name and differ only by `uniquePathSegment`.
 * [SolutionInfo.routingKey] must surface that segment so a call routes to the intended worktree rather than
 * the bare name (which the server resolves to whichever same-named solution it picks first).
 */
class ReSharperMcpTest {

    // The exact shape the live MCP returns (already unescaped once by extractText: real CRLF, single \).
    private val twoWorktrees = """
        {
          "solutionCount": 2,
          "solutions": [
            {
              "name": "Vantage",
              "path": "C:\Dev\Vantage-wt-1\Vantage.slnx",
              "toolCount": 21,
              "uniquePathSegment": "Vantage-wt-1"
            },
            {
              "name": "Vantage",
              "path": "C:\Dev\Vantage\Vantage.slnx",
              "toolCount": 21,
              "uniquePathSegment": "Vantage"
            }
          ]
        }
    """.trimIndent()

    @Test
    fun `two same-named worktree solutions are kept distinct by their unique path segment`() {
        val solutions = ReSharperMcp.parseSolutions(twoWorktrees)

        assertEquals(2, solutions.size)
        assertEquals(listOf("Vantage", "Vantage"), solutions.map { it.name })
        // The bare name collides; the routing key must not — that is what stops the cross-worktree open.
        assertEquals(listOf("Vantage-wt-1", "Vantage"), solutions.map { it.routingKey })
        assertEquals("""C:\Dev\Vantage-wt-1\Vantage.slnx""", solutions[0].path)
    }

    @Test
    fun `routing key falls back to the name when the server omits uniquePathSegment`() {
        val legacy = """{ "name": "Vantage", "path": "C:\Dev\Vantage\Vantage.slnx", "toolCount": 21 }"""

        val solutions = ReSharperMcp.parseSolutions(legacy)

        assertEquals(1, solutions.size)
        assertNull(solutions[0].uniquePathSegment)
        assertEquals("Vantage", solutions[0].routingKey)
    }

    // A C# extension-block member resolves to two declarations at one source line — the `$extension`-grouped
    // qualified name and the flattened one. The picker must show one row, not two, keeping the $extension form.
    private val extensionGrouped = SymbolHit("method", "RECTEX.\$extension.ToScreenRect", """C:\Dev\Deskmancer\RECTEX.cs""", 11)
    private val extensionFlattened = SymbolHit("method", "RECTEX.ToScreenRect", """C:\Dev\Deskmancer\RECTEX.cs""", 11)

    @Test
    fun `extension-block duplicate collapses to the $extension form`() {
        val collapsed = ReSharperMcp.collapseExtensionBlockDuplicates(listOf(extensionGrouped, extensionFlattened))

        assertEquals(listOf(extensionGrouped), collapsed)
    }

    @Test
    fun `collapse keeps a lone $extension hit even when its flattened twin is absent`() {
        val collapsed = ReSharperMcp.collapseExtensionBlockDuplicates(listOf(extensionGrouped))

        assertEquals(listOf(extensionGrouped), collapsed)
    }

    @Test
    fun `collapse leaves genuinely distinct same-name hits alone`() {
        // Two real extensions of the same name on different types: both must survive, each keeping its path.
        val sizeExtension = SymbolHit("method", "SIZEEX.\$extension.ToScreenRect", """C:\Dev\Deskmancer\SIZEEX.cs""", 9)
        val hits = listOf(extensionGrouped, sizeExtension)

        assertEquals(hits, ReSharperMcp.collapseExtensionBlockDuplicates(hits))
    }

    @Test
    fun `parseMembers skips the synthetic $extension grouping but keeps the block's real methods`() {
        val getSymbolInfo = """
            static abstract class RECTEX
            namespace: Deskmancer.Geometry.VanaraEX.PInvokeEX
            baseTypes: object

            members:
              static class ${'$'}extension(this:RECT)
              static method ToScreenRect(this:RECT) : ScreenRect
              static method From(source:ScreenRect) : RECT
        """.trimIndent()
        val names = HashSet<String>()
        val combined = HashSet<String>()

        ReSharperMcp.parseMembers(getSymbolInfo, "RECTEX", names, combined)

        assertEquals(setOf("ToScreenRect", "From"), names)
        assertEquals(setOf("RECTEX.ToScreenRect", "RECTEX.From"), combined)
    }
}
