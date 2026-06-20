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
}
