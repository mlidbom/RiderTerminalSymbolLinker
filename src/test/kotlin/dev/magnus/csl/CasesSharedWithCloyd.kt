package dev.magnus.csl

import com.google.gson.Gson
import java.io.File

/**
 * The cases of linking names to C# symbols that these tests share with Cloyd's specifications, whose chat links names
 * as the terminal does: JSON files, whose original is Cloyd's, in `~/.claude` at [CLOYDS_FOLDER]. The test resources
 * hold a copy, in [FOLDER], and [CasesSharedWithCloydTest] fails while it differs from the original.
 */
internal object CasesSharedWithCloyd {
    /** The copy's folder, among this package's test resources. */
    const val FOLDER = "cases-shared-with-cloyd"

    /** Cloyd's checkout, the Claude tooling's, which Magnus's machine has and CI's doesn't. */
    val CLOYDS_CHECKOUT = File(System.getProperty("user.home"), ".claude")

    /** The folder of the original, in [CLOYDS_CHECKOUT]. */
    val CLOYDS_FOLDER = File(CLOYDS_CHECKOUT, "src/Cloyd.Specifications/Links/CasesSharedWithTheSymbolLinker")

    /** The copy's folder, as the build put it on the tests' class path. */
    val copy: File get() = File(CasesSharedWithCloyd::class.java.getResource(FOLDER)!!.toURI())

    /** The cases in [file], one of the copy's, read as a [T]. */
    inline fun <reified T> read(file: String): T = Gson().fromJson(File(copy, file).readText(), T::class.java)
}

/**
 * A namespace that declares what the shared cases link to, with the [types] it declares; the global namespace when it
 * has no [name]. Listed as the .NET Analysis&Refactoring MCP lists it ([asListed]), it gives the [DeclaredNames] and the
 * [TypesOfNamespaces] the plugin gathers from those listings.
 */
internal class NamespaceOfTheCases(val name: String?, val types: List<TypeOfTheCases>) {
    /** It, as `browse_namespace` lists it. */
    fun asListed(): NamespaceListing = NamespaceListing(name ?: NamespaceListing.GLOBAL, emptyList(), types.map { it.asListedIn(name) }, null)

    companion object {
        /** The names [namespaces] declare, as [DeclaredNamesGatherer] gathers them from their listings. */
        fun declaredNamesIn(namespaces: List<NamespaceOfTheCases>): DeclaredNames =
            DeclaredNamesGatherer().apply { namespaces.forEach { add(it.asListed()) } }.gathered()
    }
}

/**
 * A type that the shared cases link to: its [kind], as C# writes it, such as `class` or `enum`, its [name], the names of
 * its [members], and the types nested in it.
 */
internal class TypeOfTheCases(val kind: String, val name: String, val members: List<String>?, val nestedTypes: List<TypeOfTheCases>?) {
    /** It, as `browse_namespace` lists it in [qualifier], its namespace's or nesting type's name, with its members as methods. */
    fun asListedIn(qualifier: String?): TypeListing {
        val qualifiedName = listOfNotNull(qualifier, name).joinToString(".")
        return TypeListing(
            kind, name, qualifiedName, """C:\Checkout\$name.cs""", generator = null, hintName = null, line = 1, column = 1,
            members = members.orEmpty().map { MemberListing("method", it, null) },
            nestedTypes = nestedTypes.orEmpty().map { it.asListedIn(qualifiedName) },
        )
    }
}

/** The shared cases of which C# symbols a line names, in `the-symbols-a-line-names.json`. */
internal class TheSymbolsALineNames(val declarations: List<NamespaceOfTheCases>, val cases: List<ALineAndItsLinks>)

/** One of [TheSymbolsALineNames]: its name, which reads as a specification, the [line], and the [links] it has, in order. */
internal class ALineAndItsLinks(val case: String, val line: String, val links: List<LinkOfALine>)

/**
 * A link a line of a shared case has: what it is [to], `typeOrMember` or `namespace`, and the [name] it covers, from
 * [start], the line's first character being at 0.
 */
internal class LinkOfALine(val to: String, val start: Int, val name: String) {
    val asASymbolReference: SymbolReference
        get() {
            val range = start..<start + name.length
            return when (to) {
                "typeOrMember" -> SymbolReference.ToATypeOrMember(range, name)
                "namespace" -> SymbolReference.ToANamespace(range, name)
                else -> error("A link of a shared case is to a typeOrMember or a namespace, not to \"$to\".")
            }
        }
}

/** The shared cases of which namespaces a name names, and which types a link to one leads to, in `the-types-of-a-namespace.json`. */
internal class TheTypesOfANamespace(
    val declarations: List<NamespaceOfTheCases>,
    val typesANameLeadsTo: List<ANameAndTheTypesItLeadsTo>,
    val namesOfNamespaces: List<NamesOfNamespacesOrNot>,
)

/**
 * One of [TheTypesOfANamespace.typesANameLeadsTo]: its name, which reads as a specification, the [name] as a text writes
 * it, and the qualified names of the [types] a link to the namespaces it names leads to, in order.
 */
internal class ANameAndTheTypesItLeadsTo(val case: String, val name: String, val types: List<String>)

/**
 * One of [TheTypesOfANamespace.namesOfNamespaces]: its name, which reads as a specification, the [names] as a text writes
 * them, and whether each of them names a namespace.
 */
internal class NamesOfNamespacesOrNot(val case: String, val names: List<String>, val nameANamespace: Boolean)
