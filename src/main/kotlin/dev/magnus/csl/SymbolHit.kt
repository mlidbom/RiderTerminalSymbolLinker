package dev.magnus.csl

import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.LocalFileSystem

/**
 * A declaration a link can open: [qualifiedName] as C# writes it, in [file] at [line] and [column], both 1-based.
 * [kind] is the C# keyword for what it declares, such as `class` or `method`.
 */
data class SymbolHit(val kind: String, val qualifiedName: QualifiedName, val file: String, val line: Int, val column: Int) {
    /** How the [LinkPicker] shows it: its kind, its qualified name, and its file's name and line. */
    val pickerLabel: String get() = "$kind $qualifiedName  —  ${file.substringAfterLast('\\')}:$line"

    /**
     * Opens its file with the caret on its name, taking focus when [requestFocus], and reports it to
     * [LinkClickOutcomes]. False when there's no such file to open.
     */
    fun open(project: Project, requestFocus: Boolean): Boolean {
        val virtualFile = LocalFileSystem.getInstance().findFileByPath(file.replace('\\', '/')) ?: return false
        OpenFileDescriptor(project, virtualFile, line - 1, column - 1).navigate(requestFocus)
        LinkClickOutcomes.report(LinkClickOutcome.Opened(file, line))
        return true
    }
}
