package dev.magnus.csl

import com.intellij.openapi.application.PathManager
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.project.Project
import java.nio.file.Files
import java.nio.file.Path
import java.nio.file.StandardCopyOption

/**
 * Per-solution on-disk snapshot of the [DeclaredNames], so the index is available the instant a solution opens while a
 * fresh build runs in the background (enumeration takes ~15s). Keyed by the project's stable location hash, one file
 * per solution, as plain newline-delimited qualified names.
 *
 * A namespace's line starts with [NAMESPACE_PREFIX], which no qualified name can hold, as it holds a space. A stale
 * snapshot is self-correcting: the background rebuild always overwrites it, so no explicit invalidation is needed.
 */
object SymbolIndexCache {
    private val LOG = Logger.getInstance("CSL")
    private const val FORMAT = "v2" // bump if the stored shape changes; old files are simply ignored
    private const val NAMESPACE_PREFIX = "namespace "

    private fun file(project: Project): Path =
        Path.of(PathManager.getSystemPath(), "claude-symbol-linker", "${project.locationHash}.$FORMAT.txt")

    /** The cached names for this project, or `null` if there is no usable snapshot. */
    fun load(project: Project): DeclaredNames? =
        try {
            val f = file(project)
            if (!Files.exists(f)) {
                null
            } else {
                val (namespaces, typesAndMembers) = Files.readAllLines(f).filter { it.isNotEmpty() }.partition { it.startsWith(NAMESPACE_PREFIX) }
                // A real .NET solution always has types; none means a corrupt snapshot.
                if (typesAndMembers.isEmpty()) {
                    null
                } else {
                    DeclaredNames(
                        QualifiedNames(typesAndMembers.map(::QualifiedName)),
                        QualifiedNames(namespaces.map { QualifiedName(it.removePrefix(NAMESPACE_PREFIX)) }),
                    )
                }
            }
        } catch (e: Exception) {
            LOG.warn("CSL cache read failed: ${e.message}")
            null
        }

    /** Atomically replace this project's snapshot. Failures are logged and ignored — the cache is optional. */
    fun save(project: Project, declared: DeclaredNames) {
        try {
            val f = file(project)
            Files.createDirectories(f.parent)
            val tmp = f.resolveSibling("${f.fileName}.tmp")
            val lines = declared.typesAndMembers.all.map { it.text } + declared.namespaces.all.map { "$NAMESPACE_PREFIX${it.text}" }
            Files.write(tmp, lines.asIterable())
            Files.move(tmp, f, StandardCopyOption.REPLACE_EXISTING)
        } catch (e: Exception) {
            LOG.warn("CSL cache write failed: ${e.message}")
        }
    }
}
