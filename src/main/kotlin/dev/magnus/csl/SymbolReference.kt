package dev.magnus.csl

/**
 * A name in a line of terminal output that links: where it is in the line ([range], inclusive) and the [name] as the
 * line writes it, which a click resolves.
 */
sealed interface SymbolReference {
    val range: IntRange
    val name: String

    /** A type or member, by its name, or the end of its qualified name, as `Circle.Area` or `Shapes.Circle.Area`. */
    data class ToATypeOrMember(override val range: IntRange, override val name: String) : SymbolReference

    /** A namespace, by its name, or the end of it from two of its names on, as `Geometry.Shapes`. */
    data class ToANamespace(override val range: IntRange, override val name: String) : SymbolReference
}
