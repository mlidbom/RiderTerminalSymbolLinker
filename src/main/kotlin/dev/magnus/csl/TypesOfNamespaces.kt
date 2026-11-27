package dev.magnus.csl

/**
 * The types the .NET Analysis&Refactoring MCP lists in some namespaces, by the namespace each is declared in, as a click
 * on a link to a namespace lists them ([inAndUnder]). A nested type is in the namespace of the type it's nested in.
 * A type a source generator wrote is in the file it generated ([GeneratedFile]).
 */
internal class TypesOfNamespaces(listings: List<NamespaceListing>) {
    private val byTheirNamespaces: Map<QualifiedName, List<SymbolHit>> =
        listings.associate { QualifiedName(it.name) to it.types.flatMap(::itAndTheTypesNestedIn).mapNotNull(TypeListing::toSymbolHit) }

    /**
     * The types in each of [namespaces], nested types included and not their members: the namespace's own first, then
     * those of the namespaces under it, each part by qualified name, as Magnus agreed (2026-11-26).
     */
    fun inAndUnder(namespaces: List<QualifiedName>): List<SymbolHit> =
        namespaces.sortedBy { it.text }.flatMap { namespace ->
            byQualifiedName(byTheirNamespaces[namespace].orEmpty()) +
                byQualifiedName(byTheirNamespaces.filterKeys { it.isUnder(namespace) }.values.flatten())
        }.distinct()

    private fun byQualifiedName(types: List<SymbolHit>): List<SymbolHit> = types.sortedBy { it.qualifiedName.text }

    private fun itAndTheTypesNestedIn(type: TypeListing): List<TypeListing> =
        listOf(type) + type.nestedTypes.orEmpty().flatMap(::itAndTheTypesNestedIn)
}
