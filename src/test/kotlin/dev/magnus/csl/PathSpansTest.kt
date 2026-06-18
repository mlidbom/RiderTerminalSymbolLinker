package dev.magnus.csl

import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Which spans [PathSpans] reports as file/URL links — the gate [SymbolLinkFilter] uses to avoid clobbering
 * a working file link. The bug this guards against: a separator-joined list of bare identifiers (e.g.
 * `IsSupported/CanName/CanSetPerDesktopWallpaper`) was treated as a path, so those symbols never linked.
 * A run is a path only with a real signal — extension, absolute/relative anchor, or URL scheme.
 */
class PathSpansTest {

    /** Whether any reported span overlaps the first occurrence of [token] in [line]. */
    private fun covers(line: String, token: String): Boolean {
        val i = line.indexOf(token)
        require(i >= 0) { "token '$token' not in: $line" }
        val span = i..(i + token.length - 1)
        return PathSpans.find(line).any { span.first <= it.last && it.first <= span.last }
    }

    @Test
    fun `a separator-joined identifier list is not a path, so its symbols stay linkable`() {
        // The reported regression: these are real symbols, not a file path.
        assertTrue(PathSpans.find("IsSupported/CanName/CanSetPerDesktopWallpaper").isEmpty())
    }

    @Test
    fun `a dotted member access is not mistaken for a file`() {
        assertTrue(PathSpans.find("Logger.Info").isEmpty())
    }

    @Test
    fun `a relative path to a source file is excluded, leading directories included`() {
        assertTrue(covers(".../csl/SymbolIndex.kt", "SymbolIndex")) // the file stem, inside a path
        assertTrue(covers("src/main/Program.cs", "src")) // a directory segment of a real path
    }

    @Test
    fun `a bare filename with a known extension is excluded`() {
        assertTrue(covers("see SymbolIndex.kt here", "SymbolIndex"))
    }

    @Test
    fun `an absolute windows path is excluded, with or without an extension`() {
        assertTrue(covers("""C:\Dev\Vantage\App.cs""", "Vantage"))
        assertTrue(covers("""C:\Dev\Vantage""", "Vantage")) // no extension, drive-letter anchor carries it
    }

    @Test
    fun `unix absolute and home-relative paths are excluded`() {
        assertTrue(covers("/usr/local/Foo", "Foo"))
        assertTrue(covers("~/bin/Foo", "Foo"))
    }

    @Test
    fun `a URL is excluded`() {
        assertTrue(covers("https://github.com/x/Repo", "Repo"))
    }

    @Test
    fun `plain prose reports no path spans`() {
        assertFalse(covers("call IsSupported now", "IsSupported"))
    }
}
