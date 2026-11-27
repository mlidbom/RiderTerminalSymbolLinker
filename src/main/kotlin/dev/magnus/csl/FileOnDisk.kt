package dev.magnus.csl

import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.LocalFileSystem

/** One of the solution's files on disk, by its absolute [path], with backslashes as the MCP writes it. */
data class FileOnDisk(val path: String) : SourceFile {
    override val name: String get() = path.substringAfterLast('\\')

    override fun open(project: Project, line: Int, column: Int, requestFocus: Boolean) {
        val virtualFile = LocalFileSystem.getInstance().findFileByPath(path.replace('\\', '/'))
            ?: return LinkNotices.tellNotOpened(project, "No file \"$path\" on disk")
        OpenFileDescriptor(project, virtualFile, line - 1, column - 1).navigate(requestFocus)
        LinkClickOutcomes.report(LinkClickOutcome.Opened(path, line))
    }

    override fun isOpenIn(project: Project): Boolean = OpenFiles.normalize(path) in OpenFiles.paths(project)
}
