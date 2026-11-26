package dev.magnus.csl

/**
 * Gathers a solution's [DeclaredNames] from the namespaces the .NET Analysis&Refactoring MCP lists: the qualified name
 * of each type and of each of its members, and of each namespace that declares types and of the namespaces around it.
 *
 * A nested type is gathered with its members, as the type it's nested in is. The members of a C# 14 extension block
 * count as members of the static class that declares the block.
 */
internal class DeclaredNamesGatherer {
    private val typesAndMembers = HashSet<QualifiedName>()
    private val namespaces = HashSet<QualifiedName>()

    fun add(namespace: NamespaceListing) {
        if (namespace.types.isEmpty()) return
        if (!namespace.isGlobal) namespaces += QualifiedName(namespace.name).andTheNamesItStartsWith
        namespace.types.forEach(::add)
    }

    fun gathered(): DeclaredNames = DeclaredNames(QualifiedNames(typesAndMembers), QualifiedNames(namespaces))

    private fun add(type: TypeListing) {
        typesAndMembers.add(QualifiedName(type.qualifiedName))
        type.members.orEmpty().forEach { addMember(type, it) }
        type.nestedTypes.orEmpty().forEach(::add)
    }

    private fun addMember(type: TypeListing, member: MemberListing) {
        when (member.kind) {
            EXTENSION_BLOCK -> member.members.orEmpty().forEach { addMember(type, it) }
            in KINDS_CODE_NAMES -> typesAndMembers.add(QualifiedName("${type.qualifiedName}.${member.name!!}"))
        }
    }

    private companion object {
        const val EXTENSION_BLOCK = "extension"

        /**
         * The kinds of member that code calls by the member's own name. Not constructors, which take their type's name,
         * nor indexers, which the MCP names `this`, nor operators, which keep their CLR names, such as `op_Addition`:
         * linking those words would link text that names no member.
         */
        val KINDS_CODE_NAMES = setOf("method", "property", "field", "event")
    }
}
