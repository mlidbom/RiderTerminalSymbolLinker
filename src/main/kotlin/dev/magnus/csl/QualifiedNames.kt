package dev.magnus.csl

/**
 * A set of [QualifiedName]s, found by the ends of them a text writes ([namedBy]): `Circle.Area` finds
 * `Geometry.Shapes.Circle.Area`. Looked up by their own names, so a lookup reads only the names that end alike.
 */
class QualifiedNames(names: Collection<QualifiedName>) {
    private val byTheirOwnNames: Map<String, List<QualifiedName>> = names.groupBy { it.ownName }

    /** How many names it holds. */
    val size: Int = names.size

    /** Every name it holds. */
    val all: Sequence<QualifiedName> get() = byTheirOwnNames.values.asSequence().flatten()

    /** The names that [nameAsWritten], a name or names joined by dots as a text writes them, is the end of. */
    fun namedBy(nameAsWritten: String): List<QualifiedName> =
        byTheirOwnNames[nameAsWritten.substringAfterLast('.')].orEmpty().filter { it.endsWith(nameAsWritten) }

    /** Whether [nameAsWritten] is the end of one of its names: see [namedBy]. */
    fun hasOneNamedBy(nameAsWritten: String): Boolean =
        byTheirOwnNames[nameAsWritten.substringAfterLast('.')].orEmpty().any { it.endsWith(nameAsWritten) }

    companion object {
        val NONE = QualifiedNames(emptyList())
    }
}
