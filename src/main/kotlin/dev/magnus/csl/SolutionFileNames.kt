package dev.magnus.csl

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project

/**
 * Project-scoped holder of the current [SolutionFileIndex] — the file-side analogue of [SymbolIndex] and
 * the sole authority on what the [FileLinkFilter] links. Built once at startup from the platform's
 * content index (see [SolutionFileIndexLoader]); until [isReady] the filter links nothing. The matching
 * rules themselves live in [SolutionFileIndex]; this just snapshots and serves them to the project.
 */
@Service(Service.Level.PROJECT)
class SolutionFileNames {
    @Volatile
    private var index: SolutionFileIndex = SolutionFileIndex.EMPTY

    @Volatile
    var isReady: Boolean = false
        private set

    /** Whether [path]'s full segment sequence is a trailing path of a real solution file. The link gate. */
    fun matches(path: String): Boolean = index.matches(path)

    /** Solution files [path] is a true trailing-path of; see [SolutionFileIndex.resolve]. */
    fun resolve(path: String): List<String> = index.resolve(path)

    fun set(byName: Map<String, List<String>>) {
        index = SolutionFileIndex(byName)
        isReady = true
    }

    companion object {
        fun getInstance(project: Project): SolutionFileNames = project.service()
    }
}
