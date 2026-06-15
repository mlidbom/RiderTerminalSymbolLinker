package dev.magnus.csl

import com.intellij.openapi.application.PathManager
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.project.Project
import java.nio.file.Files
import java.nio.file.Path
import java.nio.file.StandardCopyOption

/**
 * Per-solution on-disk snapshot of the [SymbolNames] gates, so the index is available the instant a
 * solution opens while a fresh build runs in the background (enumeration takes ~15s). Keyed by the
 * project's stable location hash — one file per solution — as plain newline-delimited names.
 *
 * Both gates share one flat file: a short name is a single identifier and never contains a dot, a
 * combined `SimpleType.Member` name always does, so the two are recovered on load by partitioning on
 * the dot — no header or section markers needed. A stale snapshot is self-correcting: the background
 * rebuild always overwrites it, so no explicit invalidation is needed.
 */
object SymbolIndexCache {
    private val LOG = Logger.getInstance("CSL")
    private const val FORMAT = "v1" // bump if the stored shape changes; old files are simply ignored

    private fun file(project: Project): Path =
        Path.of(PathManager.getSystemPath(), "claude-symbol-linker", "${project.locationHash}.$FORMAT.txt")

    /** The cached gates for this project, or `null` if there is no usable snapshot. */
    fun load(project: Project): SymbolNames? =
        try {
            val f = file(project)
            if (!Files.exists(f)) {
                null
            } else {
                val short = HashSet<String>()
                val combined = HashSet<String>()
                for (name in Files.readAllLines(f)) {
                    if (name.isEmpty()) continue
                    if ('.' in name) combined.add(name) else short.add(name)
                }
                // A real .NET solution always has short names; only combined ones means a corrupt snapshot.
                if (short.isEmpty()) null else SymbolNames(short, combined)
            }
        } catch (e: Exception) {
            LOG.warn("CSL cache read failed: ${e.message}")
            null
        }

    /** Atomically replace this project's snapshot. Failures are logged and ignored — the cache is optional. */
    fun save(project: Project, symbols: SymbolNames) {
        try {
            val f = file(project)
            Files.createDirectories(f.parent)
            val tmp = f.resolveSibling("${f.fileName}.tmp")
            Files.write(tmp, symbols.short + symbols.combined)
            Files.move(tmp, f, StandardCopyOption.REPLACE_EXISTING)
        } catch (e: Exception) {
            LOG.warn("CSL cache write failed: ${e.message}")
        }
    }
}
