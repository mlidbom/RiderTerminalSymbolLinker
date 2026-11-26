package dev.magnus.csl

/**
 * A C# name with what qualifies it, its names joined by dots: a type's or a member's after those of its namespace and
 * the types it is in, `Geometry.Shapes.Circle.Area`, or a namespace's, `Geometry.Shapes`. A text that names a symbol or
 * a namespace writes the end of it, from one of its names ([endsWith]).
 */
@JvmInline
value class QualifiedName(val text: String) {
    /** Its last name, the one its declaration gives it: `Area` of `Geometry.Shapes.Circle.Area`. */
    val ownName: String get() = text.substringAfterLast('.')

    /**
     * Whether [nameAsWritten], a name or names joined by dots as a text writes them, is its end from one of its names:
     * `Area`, `Circle.Area` or all of it, but not `ircle.Area`.
     */
    fun endsWith(nameAsWritten: String): Boolean =
        text.endsWith(nameAsWritten) && (text.length == nameAsWritten.length || text[text.length - nameAsWritten.length - 1] == '.')

    /**
     * It and the names it starts with, from its first name on: `Geometry`, then `Geometry.Shapes`. For a namespace, the
     * namespaces it is in and it.
     */
    val andTheNamesItStartsWith: List<QualifiedName>
        get() = text.split('.').runningReduce { qualifier, name -> "$qualifier.$name" }.map(::QualifiedName)

    /** Whether it starts with all of [other]'s names and has more: a namespace or a type in [other], or under it. */
    fun isUnder(other: QualifiedName): Boolean =
        text.length > other.text.length && text.startsWith(other.text) && text[other.text.length] == '.'

    override fun toString(): String = text
}
