package dev.magnus.csl

import com.google.gson.JsonParser
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test
import java.io.File

/** Reading the .NET Analysis&Refactoring MCP's answers: its tools' JSON form, and its `GET /health`. */
class DotNetAnalysisAndRefactoringMcpTest {

    @Test
    fun `the MCP serves a solution its health lists by its file's path, and not another in the same folder`() {
        val health = """{ "solutions": [ { "name": "Compze.AllProjects", "path": "C:\\Dev\\Compze\\Compze.AllProjects.slnx", "indexing": false } ], "indexing": false }"""

        assertEquals(true, DotNetAnalysisAndRefactoringMcp.healthServes(health, File("""C:\Dev\Compze\Compze.AllProjects.slnx""")))
        assertEquals(false, DotNetAnalysisAndRefactoringMcp.healthServes(health, File("""C:\Dev\Compze\Compze.Threading.slnx""")))
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

    @Test
    fun `a call the MCP refuses, in text flagged as an error, has no answer`() =
        assertNull(
            DotNetAnalysisAndRefactoringMcp.toolAnswerIn(
                "browse_namespace",
                """{ "jsonrpc": "2.0", "id": 1, "result": { "content": [{ "type": "text", "text": "Solution 'Mavalonia' not found." }], "isError": true } }""",
            ),
        )

    @Test
    fun `a tool's answer is the JSON object in its text`() =
        assertEquals(
            json("""{ "declarations": [] }"""),
            DotNetAnalysisAndRefactoringMcp.toolAnswerIn(
                "go_to_definition",
                """{ "jsonrpc": "2.0", "id": 1, "result": { "content": [{ "type": "text", "text": "{ \"declarations\": [] }" }] } }""",
            ),
        )

    private fun json(text: String) = JsonParser.parseString(text.trimIndent()).asJsonObject
}
