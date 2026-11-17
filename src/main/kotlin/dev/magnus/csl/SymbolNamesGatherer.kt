package dev.magnus.csl

/**
 * Gathers a solution's [SymbolNames] from the types the .NET Analysis&Refactoring MCP lists: each type's name and its
 * members' names as short names, and each member paired with its type's name as a combined `Type.Member`, so that a
 * dotted access in the terminal links only when that member-of-type pair exists.
 *
 * A nested type counts as a member of the type it's nested in, and its own members are gathered too. The members of
 * a C# 14 extension block count as members of the static class that declares the block.
 */
internal class SymbolNamesGatherer {
    private val short = HashSet<String>()
    private val combined = HashSet<String>()

    fun add(type: TypeListing) {
        short.add(type.name)
        type.members.orEmpty().forEach { addMember(type, it) }
        type.nestedTypes.orEmpty().forEach { nested ->
            addMemberName(type, nested.name)
            add(nested)
        }
    }

    fun gathered(): SymbolNames = SymbolNames(short, combined)

    private fun addMember(type: TypeListing, member: MemberListing) {
        when (member.kind) {
            EXTENSION_BLOCK -> member.members.orEmpty().forEach { addMember(type, it) }
            in KINDS_CODE_NAMES -> addMemberName(type, member.name!!)
        }
    }

    private fun addMemberName(type: TypeListing, name: String) {
        short.add(name)
        combined.add("${type.name}.$name")
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
