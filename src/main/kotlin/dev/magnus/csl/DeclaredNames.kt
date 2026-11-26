package dev.magnus.csl

/**
 * The names a solution declares that link in terminal output, by their [QualifiedName]s: those of its types and members
 * ([typesAndMembers]), and those of the namespaces its types are declared in and of the namespaces around them
 * ([namespaces]), such as `Geometry` around `Geometry.Shapes`.
 */
class DeclaredNames(val typesAndMembers: QualifiedNames, val namespaces: QualifiedNames) {
    /**
     * Whether [nameAsWritten], a name or names joined by dots as a text writes them, names a type or member: its name,
     * or the end of its qualified name from one of its names, as `Area`, `Circle.Area` and `Geometry.Shapes.Circle.Area`
     * name `Geometry.Shapes.Circle.Area`.
     */
    fun namesATypeOrMember(nameAsWritten: String): Boolean = typesAndMembers.hasOneNamedBy(nameAsWritten)

    /** Whether [nameAsWritten] names a namespace: its name, or the end of it from one of its names. */
    fun namesANamespace(nameAsWritten: String): Boolean = namespaces.hasOneNamedBy(nameAsWritten)

    /** The namespaces [nameAsWritten] names: see [namesANamespace]. */
    fun namespacesNamedBy(nameAsWritten: String): List<QualifiedName> = namespaces.namedBy(nameAsWritten)

    companion object {
        val NONE = DeclaredNames(QualifiedNames.NONE, QualifiedNames.NONE)
    }
}
