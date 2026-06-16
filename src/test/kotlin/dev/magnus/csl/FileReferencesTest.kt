package dev.magnus.csl

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Tokenization, boundary and line-suffix parsing of [FileReferences.find]. The gate here is a stub that
 * only checks the file name, so these tests isolate *how the span is carved out* (path validation against
 * a real index is covered by [SolutionFileIndexTest]). The key contract proven below: `find` hands the
 * gate the entire path-shaped run, so the real gate can reject a bogus leading segment.
 */
class FileReferencesTest {

    private val knownNames = setOf("appshell.cs", "app.axaml.cs", "program.cs")

    private fun find(line: String): List<FileReferences.Ref> =
        FileReferences.find(line) { path ->
            path.substringAfterLast('/').substringAfterLast('\\').lowercase() in knownNames
        }

    private fun single(line: String): FileReferences.Ref {
        val refs = find(line)
        assertEquals(1, refs.size, "expected exactly one reference in: $line")
        return refs.first()
    }

    @Test
    fun `bare name with a line range`() {
        val ref = single("App.axaml.cs:63-71")
        assertEquals("App.axaml.cs", ref.path)
        assertEquals("App.axaml.cs", ref.fileName)
        assertEquals(63, ref.startLine)
        assertEquals(71, ref.endLine)
        assertNull(ref.column)
        assertEquals(0..17, ref.range) // the link spans the path and the :63-71 suffix
    }

    @Test
    fun `line only and line-with-column suffixes`() {
        val lineOnly = single("App.axaml.cs:63")
        assertEquals(63, lineOnly.startLine)
        assertNull(lineOnly.endLine)
        assertNull(lineOnly.column)

        val lineCol = single("App.axaml.cs:63:5")
        assertEquals(63, lineCol.startLine)
        assertNull(lineCol.endLine)
        assertEquals(5, lineCol.column)
    }

    @Test
    fun `GitHub line anchor as Rider's add-to-chat emits`() {
        // The exact shape Rider injects into the chat prompt: a leading @ and a #L<start>-<end> anchor.
        val ref = single("@Vantage\\Shell\\AppShell.cs#L136-138")
        assertEquals("Vantage\\Shell\\AppShell.cs", ref.path)
        assertEquals("AppShell.cs", ref.fileName)
        assertEquals(136, ref.startLine)
        assertEquals(138, ref.endLine)
        assertNull(ref.column)
        assertEquals(1, ref.range.first) // the leading @ is a boundary, excluded from the link
        assertEquals("@Vantage\\Shell\\AppShell.cs#L136-138".length - 1, ref.range.last) // anchor included
    }

    @Test
    fun `GitHub anchor single line and double-L range forms`() {
        val singleLine = single("App.axaml.cs#L63")
        assertEquals(63, singleLine.startLine)
        assertNull(singleLine.endLine)
        assertNull(singleLine.column)

        val doubleL = single("App.axaml.cs#L63-L71")
        assertEquals(63, doubleL.startLine)
        assertEquals(71, doubleL.endLine)
    }

    @Test
    fun `a bare # before a path is still a boundary, not a line anchor`() {
        // Only a #L<digit> anchor directly after a path is absorbed; a leading # remains a boundary.
        assertEquals("Vantage/Shell/AppShell.cs", single("#Vantage/Shell/AppShell.cs").path)
    }

    @Test
    fun `trailing sentence punctuation is not part of the link`() {
        val ref = single("see App.axaml.cs:63.")
        assertEquals("App.axaml.cs", ref.path)
        assertEquals(63, ref.startLine)
        assertEquals(4..18, ref.range) // excludes the leading "see " and the trailing '.'
    }

    @Test
    fun `partial and absolute paths keep their original separators`() {
        assertEquals("Vantage\\Shell\\AppShell.cs", single("Vantage\\Shell\\AppShell.cs").path)
        assertEquals("C:\\Dev\\x\\AppShell.cs", single("C:\\Dev\\x\\AppShell.cs").path)
    }

    @Test
    fun `a reference glued to surrounding parentheses is isolated`() {
        val ref = single("Update(C:\\Dev\\x\\AppShell.cs)")
        assertEquals("C:\\Dev\\x\\AppShell.cs", ref.path)
    }

    @Test
    fun `a non-path character before the path is a boundary, excluded from the link`() {
        assertEquals("Vantage/Shell/AppShell.cs", single("#Vantage/Shell/AppShell.cs").path)
        assertEquals("Vantage/Shell/AppShell.cs", single("%Vantage/Shell/AppShell.cs").path)
        assertEquals("Vantage/Shell/AppShell.cs", single("->Vantage/Shell/AppShell.cs").path)
    }

    @Test
    fun `the entire path-shaped run is handed to the gate`() {
        // The stub accepts on file name, so this links; the point is the captured path includes the bogus
        // leading segment, which is exactly what lets the real gate reject it.
        assertEquals("nonexistingVantage/Shell/AppShell.cs", single("nonexistingVantage/Shell/AppShell.cs").path)
    }

    @Test
    fun `URLs are left alone`() {
        assertTrue(find("https://github.com/x/AppShell.cs").isEmpty())
    }

    @Test
    fun `lines without a known file produce nothing`() {
        assertTrue(find("just some prose here").isEmpty())
        assertTrue(find("see RandomThing.cs over there").isEmpty())
    }
}
