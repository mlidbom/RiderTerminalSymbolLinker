package dev.magnus.csl

import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.project.Project

/**
 * The files currently open in editor tabs, used to emphasize them in the disambiguation pickers.
 * Paths are normalized to one shape on both sides — forward slashes, lowercased (Windows file systems
 * are case-insensitive) — so a candidate path compares equal regardless of how it was written.
 */
object OpenFiles {
    /** Normalized paths of every file open in an editor tab right now. */
    fun paths(project: Project): Set<String> =
        FileEditorManager.getInstance(project).openFiles.mapTo(HashSet()) { normalize(it.path) }

    fun normalize(path: String): String = path.replace('\\', '/').lowercase()
}
