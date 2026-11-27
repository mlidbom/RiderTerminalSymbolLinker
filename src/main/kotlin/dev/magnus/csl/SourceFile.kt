package dev.magnus.csl

import com.intellij.openapi.project.Project

/**
 * The file a declaration is in: one of the solution's files on disk ([FileOnDisk]), or one a source generator wrote
 * ([GeneratedFile]), which Rider holds in memory until it opens it.
 */
sealed interface SourceFile {
    /** Its name, as the [LinkPicker] shows it, such as `Circle.cs`. */
    val name: String

    /**
     * Opens it in an editor with the caret at [line] and [column], both 1-based, taking focus when [requestFocus], and
     * reports it to [LinkClickOutcomes]; or tells the user why it can't ([LinkNotices.tellNotOpened]).
     */
    fun open(project: Project, line: Int, column: Int, requestFocus: Boolean)

    /** Whether it is open in an editor tab, for the [LinkPicker] to mark it. */
    fun isOpenIn(project: Project): Boolean

    companion object {
        /**
         * The file the .NET Analysis&Refactoring MCP says a declaration is in: [file] on disk, or the file [generator]
         * wrote and named [hintName]. Null for a compiled symbol, which has neither.
         */
        internal fun of(file: String?, generator: String?, hintName: String?): SourceFile? = when {
            file != null -> FileOnDisk(file)
            generator != null && hintName != null -> GeneratedFile(generator, hintName)
            else -> null
        }
    }
}
