package dev.magnus.csl

import com.intellij.openapi.project.Project

/**
 * A declaration a link can open: [qualifiedName] as C# writes it, in [file] at [line] and [column], both 1-based.
 * [kind] is the C# keyword for what it declares, such as `class` or `method`.
 */
data class SymbolHit(val kind: String, val qualifiedName: QualifiedName, val file: SourceFile, val line: Int, val column: Int) {
    /** How the [LinkPicker] shows it: its kind, its qualified name, and its file's name and line. */
    val pickerLabel: String get() = "$kind $qualifiedName  —  ${file.name}:$line"

    /** Opens its file with the caret on its name, taking focus when [requestFocus], as [SourceFile.open] does. */
    fun open(project: Project, requestFocus: Boolean) = file.open(project, line, column, requestFocus)
}
