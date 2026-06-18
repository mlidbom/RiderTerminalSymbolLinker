package dev.magnus.csl

/**
 * The spans of a console line that Rider's native terminal — and the plugin's own [FileLinkFilter] —
 * already turn into file or URL links. [SymbolLinkFilter] consults these so it never overwrites a working
 * file link with a symbol link (re-linkifying "SymbolIndex" inside ".../csl/SymbolIndex.kt" would replace
 * the file link with a worse one).
 *
 * A separator-containing run is treated as a path/URL only when it carries a real path signal: a
 * whitelisted file extension, an absolute/relative anchor (drive letter, leading separator, `~/`, `./`,
 * `../`), or a URL scheme. A bare run of identifiers merely joined by separators — e.g.
 * `IsSupported/CanName/CanSetPerDesktopWallpaper` — is not a path Rider linkifies, so it is left alone and
 * its segments stay available as symbol links. (Treating any interior separator as proof of a path was the
 * bug that hid such symbols.)
 */
object PathSpans {

    /** Extensions that mark a token as a file reference, so a qualified name like `Logger.Info` is not. */
    private const val FILE_EXTENSIONS =
        "cs|csproj|sln|slnx|kt|kts|java|xml|json|ya?ml|txt|md|props|targets|config|" +
            "resx|razor|cshtml|js|ts|tsx|jsx|html?|css|py|go|rs|rb|sql|sh|ps1|gradle|properties|toml|ini"

    /** A bare "<name>.<ext>" reference (no directory), optionally trailed by ":line[:col]". */
    private val fileNameWithExtension = Regex("""[\w.\-]+\.(?:$FILE_EXTENSIONS)(?::\d+(?::\d+)?)?""")

    /** A maximal run containing a directory separator — a candidate path or URL. */
    private val separatorRun = Regex("""[\w.:~\-]*[/\\][\w.:/\\~\-]*""")

    /** A URL scheme prefix (`https://`, `file://`, …). */
    private val urlScheme = Regex("""^[A-Za-z][A-Za-z0-9+.\-]*://""")

    /** An absolute or relative path anchor: drive letter, leading separator, `~/`, `./` or `../`. */
    private val pathAnchor = Regex("""^(?:[A-Za-z]:[/\\]|[/\\]|~[/\\]|\.{1,2}[/\\])""")

    /** Inclusive offset ranges of [line] that are file/URL links, which a symbol link must not overwrite. */
    fun find(line: String): List<IntRange> =
        (separatorRun.findAll(line).filter { isPathOrUrl(it.value) } + fileNameWithExtension.findAll(line))
            .map { it.range }
            .toList()

    /** Whether a separator-containing run is a real path/URL rather than a separator-joined identifier list. */
    private fun isPathOrUrl(run: String): Boolean =
        urlScheme.containsMatchIn(run) || pathAnchor.containsMatchIn(run) || fileNameWithExtension.containsMatchIn(run)
}
