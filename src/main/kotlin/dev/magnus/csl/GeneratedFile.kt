package dev.magnus.csl

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.fileEditor.FileEditorManagerListener
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.platform.backend.workspace.WorkspaceModel
import com.intellij.util.concurrency.AppExecutorUtil
import com.jetbrains.rider.model.RdSourceGeneratorFileDescriptor
import com.jetbrains.rider.projectView.views.navigateToFileViaBackend
import com.jetbrains.rider.projectView.workspace.ProjectModelEntity
import java.util.concurrent.TimeUnit

/**
 * A file a source generator wrote, which Rider holds in memory, as the .NET Analysis&Refactoring MCP names it: by its
 * [generator], the generator's full type name, and its [hintName], the name the generator gave it, such as `Ruler.g.cs`.
 *
 * Rider's project model lists it under its project's source generators, and Rider opens it as Solution Explorer does:
 * its backend writes it to a file of its own, under `%TEMP%\SourceGeneratedDocuments`, and opens that in an editor at
 * its start. Once the editor has opened it, the caret is put at the declaration.
 *
 * For some seconds after the solution opens, the project model lists the file without where it is, or not at all,
 * until Rider's backend has run the generator: a click then says so.
 */
data class GeneratedFile(val generator: String, val hintName: String) : SourceFile {
    override val name: String get() = hintName

    override fun open(project: Project, line: Int, column: Int, requestFocus: Boolean) {
        val entry = entryIn(project)
        val path = entry?.url?.presentableUrl
        if (entry == null || path == null) return LinkNotices.tellNotOpened(project, "Rider hasn't generated $hintName yet: click again in a moment")
        val openAlready = openFileAt(project, path)
        if (openAlready != null) {
            putTheCaret(project, openAlready, line, column, requestFocus)
        } else {
            whenAnEditorOpens(project, path) { putTheCaret(project, it, line, column, requestFocus) }
            entry.navigateToFileViaBackend(project)
        }
    }

    override fun isOpenIn(project: Project): Boolean =
        entryIn(project)?.url?.presentableUrl?.let { openFileAt(project, it) } != null

    /** Its entry in Rider's project model: the file named [hintName] under the node of its [generator]. */
    private fun entryIn(project: Project): ProjectModelEntity? =
        entriesIn(project).firstOrNull { it.name == hintName && it.parentEntity?.name == generator }

    private fun openFileAt(project: Project, path: String): VirtualFile? =
        FileEditorManager.getInstance(project).openFiles.firstOrNull { OpenFiles.normalize(it.path) == OpenFiles.normalize(path) }

    /**
     * Runs [then] with the file at [path] once an editor opens it, if one does within
     * [SECONDS_THE_BACKEND_MAY_TAKE_TO_OPEN_IT]. It runs after the backend's own placing of the caret, at the file's start.
     */
    private fun whenAnEditorOpens(project: Project, path: String, then: (VirtualFile) -> Unit) {
        val connection = project.messageBus.connect()
        connection.subscribe(FileEditorManagerListener.FILE_EDITOR_MANAGER, object : FileEditorManagerListener {
            override fun fileOpened(source: FileEditorManager, file: VirtualFile) {
                if (OpenFiles.normalize(file.path) != OpenFiles.normalize(path)) return
                connection.disconnect()
                ApplicationManager.getApplication().invokeLater { then(file) }
            }
        })
        AppExecutorUtil.getAppScheduledExecutorService().schedule(connection::disconnect, SECONDS_THE_BACKEND_MAY_TAKE_TO_OPEN_IT, TimeUnit.SECONDS)
    }

    private fun putTheCaret(project: Project, file: VirtualFile, line: Int, column: Int, requestFocus: Boolean) {
        OpenFileDescriptor(project, file, line - 1, column - 1).navigate(requestFocus)
        LinkClickOutcomes.report(LinkClickOutcome.Opened(file.path, line))
    }

    companion object {
        private const val SECONDS_THE_BACKEND_MAY_TAKE_TO_OPEN_IT = 10L

        /**
         * The entries in Rider's project model of the files source generators wrote, each with its
         * [ProjectModelEntity.url] once Rider knows where the file is.
         */
        internal fun entriesIn(project: Project): Sequence<ProjectModelEntity> =
            WorkspaceModel.getInstance(project).currentSnapshot.entities(ProjectModelEntity::class.java)
                .filter { it.descriptor is RdSourceGeneratorFileDescriptor }
    }
}
