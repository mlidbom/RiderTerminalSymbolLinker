package dev.magnus.csl

import com.google.gson.JsonParser
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

/**
 * Reading the JSON form of the .NET Analysis&Refactoring MCP's answers. For `list_solutions` the point is the worktree
 * collision: two git worktrees of one repo load two solutions that share a name and differ only by
 * `uniquePathSegment`, which [SolutionInfo.routingKey] must surface, so that a call routes to the intended worktree.
 */
class DotNetAnalysisAndRefactoringMcpTest {

    @Test
    fun `two same-named worktree solutions are kept distinct by their unique path segment`() {
        val solutions = DotNetAnalysisAndRefactoringMcp.solutionsIn(
            json(
                """
                {
                  "solutionCount": 2,
                  "solutions": [
                    { "name": "Vantage", "path": "C:\\Dev\\Vantage-wt-1\\Vantage.slnx", "toolCount": 21, "uniquePathSegment": "Vantage-wt-1" },
                    { "name": "Vantage", "path": "C:\\Dev\\Vantage\\Vantage.slnx", "toolCount": 21, "uniquePathSegment": "Vantage" }
                  ]
                }
                """,
            ),
        )

        assertEquals(listOf("Vantage", "Vantage"), solutions.map { it.name })
        // The bare name collides; the routing key must not — that is what stops the cross-worktree open.
        assertEquals(listOf("Vantage-wt-1", "Vantage"), solutions.map { it.routingKey })
        assertEquals("""C:\Dev\Vantage-wt-1\Vantage.slnx""", solutions[0].path)
    }

    @Test
    fun `routing key falls back to the name when the server leaves out uniquePathSegment`() {
        val solutions = DotNetAnalysisAndRefactoringMcp.solutionsIn(
            json("""{ "solutionCount": 1, "solutions": [{ "name": "Vantage", "path": "C:\\Dev\\Vantage\\Vantage.slnx", "toolCount": 21 }] }"""),
        )

        assertNull(solutions.single().uniquePathSegment)
        assertEquals("Vantage", solutions.single().routingKey)
    }

    @Test
    fun `every declaration with source is a hit, and a compiled one is left out`() {
        val hits = DotNetAnalysisAndRefactoringMcp.declarationsIn(
            json(
                """
                {
                  "declarations": [
                    { "kind": "record", "name": "Bounds", "qualifiedName": "Geometry.Layout.Bounds", "file": "C:\\Fixture\\Layout\\Bounds.cs", "line": 4, "column": 15 },
                    { "kind": "record", "name": "Bounds", "qualifiedName": "Geometry.Shapes.Bounds", "file": "C:\\Fixture\\Shapes\\Bounds.cs", "line": 4, "column": 15 },
                    { "kind": "struct", "name": "Bounds", "qualifiedName": "Some.Library.Bounds", "file": null, "line": null, "column": null, "assembly": "Some.Library.dll" }
                  ]
                }
                """,
            ),
        )

        assertEquals(
            listOf(
                SymbolHit("record", QualifiedName("Geometry.Layout.Bounds"), """C:\Fixture\Layout\Bounds.cs""", 4, 15),
                SymbolHit("record", QualifiedName("Geometry.Shapes.Bounds"), """C:\Fixture\Shapes\Bounds.cs""", 4, 15),
            ),
            hits,
        )
    }

    private fun json(text: String) = JsonParser.parseString(text.trimIndent()).asJsonObject
}
