package dev.magnus.csl

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * The path-matching rules: a reference links only when its whole path is a real trailing path of a
 * solution file. Keys are lowercased file names (as [SolutionFileIndexLoader] stores them); values are
 * full VFS-style paths.
 */
class SolutionFileIndexTest {

    private val index = SolutionFileIndex(
        mapOf(
            "appshell.cs" to listOf("C:/Dev/Virtual-Desktop-Grid-Switcher/Vantage/Shell/AppShell.cs"),
            "app.axaml.cs" to listOf("C:/Dev/Proj/App.axaml.cs"),
            // Same name in two folders — exercises disambiguation.
            "program.cs" to listOf("C:/Dev/Proj/Client/Program.cs", "C:/Dev/Proj/Server/Program.cs"),
        ),
    )

    @Test
    fun `bare file name matches`() {
        assertTrue(index.matches("AppShell.cs"))
        assertTrue(index.matches("App.axaml.cs"))
    }

    @Test
    fun `matching is case-insensitive`() {
        assertTrue(index.matches("APPSHELL.CS"))
        assertTrue(index.matches("appshell.cs"))
    }

    @Test
    fun `partial paths that are true suffixes match`() {
        assertTrue(index.matches("Shell/AppShell.cs"))
        assertTrue(index.matches("Vantage/Shell/AppShell.cs"))
    }

    @Test
    fun `full and absolute paths match, either slash direction`() {
        assertTrue(index.matches("C:/Dev/Virtual-Desktop-Grid-Switcher/Vantage/Shell/AppShell.cs"))
        assertTrue(index.matches("C:\\Dev\\Virtual-Desktop-Grid-Switcher\\Vantage\\Shell\\AppShell.cs"))
        assertTrue(index.matches("Vantage\\Shell\\AppShell.cs"))
    }

    @Test
    fun `a path with a non-existent leading segment does not match`() {
        assertFalse(index.matches("nonexisting/Vantage/Shell/AppShell.cs"))
    }

    @Test
    fun `a wrong first segment glued to the real path does not match`() {
        assertFalse(index.matches("nonexistingVantage/Shell/AppShell.cs"))
    }

    @Test
    fun `a real name under the wrong folder does not match`() {
        assertFalse(index.matches("Client/AppShell.cs"))
    }

    @Test
    fun `an unknown name does not match`() {
        assertFalse(index.matches("DoesNotExist.cs"))
        assertFalse(index.matches(""))
    }

    @Test
    fun `a bare name shared by several files resolves to all of them`() {
        assertEquals(2, index.resolve("Program.cs").size)
    }

    @Test
    fun `a path prefix pins the one matching file`() {
        assertEquals(listOf("C:/Dev/Proj/Client/Program.cs"), index.resolve("Client/Program.cs"))
        assertEquals(listOf("C:/Dev/Proj/Server/Program.cs"), index.resolve("Server/Program.cs"))
        assertTrue(index.resolve("Other/Program.cs").isEmpty())
    }
}
