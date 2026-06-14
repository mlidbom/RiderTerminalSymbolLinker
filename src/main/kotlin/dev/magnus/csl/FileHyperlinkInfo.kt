package dev.magnus.csl

import com.intellij.execution.filters.HyperlinkInfo
import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.LocalFileSystem

/**
 * On click, resolve a file reference to the actual solution file(s) and open it. Resolution mirrors the
 * symbol side ([SymbolHyperlinkInfo]):
 *  - one matching file   -> open it
 *  - several             -> a picker of candidate paths (the same [LinkPicker] symbols use)
 *  - none                -> brief "no file" notice (only if the index went stale after the link was drawn)
 *
 * [SolutionFileNames.resolve] already returns only files whose path the reference is a true suffix of, so
 * the candidates here all match fully — a partial or absolute path that pins one file opens directly, and
 * a bare name shared by several files shows the picker. No further ranking is needed.
 *
 * A `:start-end` range selects those lines in the opened editor; a bare `:line` positions the caret.
 * Everything runs on the EDT — resolution is an in-memory lookup against the snapshot, no I/O — so a
 * Ctrl-click in the picker can open in the background just as it does for symbols.
 */
class FileHyperlinkInfo(
    private val path: String,
    private val fileName: String,
    private val startLine: Int?,
    private val endLine: Int?,
    private val column: Int?,
) : HyperlinkInfo {

    override fun navigate(project: Project) {
        val candidates = SolutionFileNames.getInstance(project).resolve(path)
        when {
            candidates.isEmpty() -> notifyNoFile(project)
            candidates.size == 1 -> open(project, candidates.first())
            else -> showPicker(project, candidates)
        }
    }

    /** [requestFocus] false opens in the background, leaving focus on the picker (for Ctrl-click). */
    private fun open(project: Project, filePath: String, requestFocus: Boolean = true) {
        val file = LocalFileSystem.getInstance().findFileByPath(filePath) ?: return notifyNoFile(project)
        val line = ((startLine ?: 1) - 1).coerceAtLeast(0)
        val col = ((column ?: 1) - 1).coerceAtLeast(0)
        OpenFileDescriptor(project, file, line, col).navigate(requestFocus)
        if (startLine != null && endLine != null) selectRange(project, file)
    }

    /** Select [startLine]..[endLine] in the just-opened editor, so the referenced block is highlighted. */
    private fun selectRange(project: Project, file: com.intellij.openapi.vfs.VirtualFile) {
        val editor = FileEditorManager.getInstance(project).selectedTextEditor ?: return
        if (FileDocumentManager.getInstance().getFile(editor.document) != file) return
        val doc = editor.document
        val first = (minOf(startLine!!, endLine!!) - 1).coerceIn(0, doc.lineCount - 1)
        val last = (maxOf(startLine, endLine) - 1).coerceIn(0, doc.lineCount - 1)
        val from = doc.getLineStartOffset(first)
        val to = doc.getLineEndOffset(last)
        editor.selectionModel.setSelection(from, to)
        editor.caretModel.moveToOffset(from)
    }

    private fun showPicker(project: Project, paths: List<String>) =
        LinkPicker.show(project, "Files named \"$fileName\"", paths, { label(project, it) }) { p, focus ->
            open(project, p, focus)
        }

    /** Project-relative path when the file is under the project, else the full path. */
    private fun label(project: Project, filePath: String): String {
        val base = project.basePath?.replace('\\', '/') ?: return filePath
        val normalized = filePath.replace('\\', '/')
        return if (normalized.startsWith("$base/")) normalized.removePrefix("$base/") else filePath
    }

    private fun notifyNoFile(project: Project) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("RiderTerminalSymbolLinker")
            .createNotification("No file \"$fileName\" found", NotificationType.INFORMATION)
            .notify(project)
    }
}
