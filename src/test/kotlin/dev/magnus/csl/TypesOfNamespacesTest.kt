package dev.magnus.csl

import com.google.gson.Gson
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.DynamicTest.dynamicTest
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.TestFactory

/**
 * Which namespaces a name names ([DeclaredNames.namespacesNamedBy]), and which types a click on a link to one lists
 * ([TypesOfNamespaces.inAndUnder]): its own first, then those of the namespaces under it, nested types included and
 * members not, as Magnus agreed (2026-11-26). Most cases are shared with Cloyd's specifications
 * ([CasesSharedWithCloyd]), each named as a specification; those of the MCP's listings alone are here.
 */
class TypesOfNamespacesTest {

    private val shared = CasesSharedWithCloyd.read<TheTypesOfANamespace>("the-types-of-a-namespace.json")

    private val declared = NamespaceOfTheCases.declaredNamesIn(shared.declarations)

    private val sharedTypes = TypesOfNamespaces(shared.declarations.map { it.asListed() })

    @TestFactory
    fun `the cases of the types a name leads to, shared with Cloyd`(): List<DynamicTest> = shared.typesANameLeadsTo.map { name ->
        dynamicTest(name.case) { assertEquals(name.types, sharedTypes.inAndUnder(declared.namespacesNamedBy(name.name)).map { it.qualifiedName.text }) }
    }

    @TestFactory
    fun `the cases of the names of namespaces, shared with Cloyd`(): List<DynamicTest> = shared.namesOfNamespaces.map { names ->
        dynamicTest(names.case) { assertEquals(emptyList<String>(), names.names.filter { declared.namesANamespace(it) != names.nameANamespace }) }
    }

    private val listedTypes = TypesOfNamespaces(
        listOf(
            listing("Shop.Orders.Shipping", type("class", "Shop.Orders.Shipping.Parcel")),
            listing(
                "Shop.Billing",
                type("class", "Shop.Billing.Invoice"),
                type("class", "Shop.Billing.Receipt", file = null, generatedAs = "Receipt.g.cs"),
            ),
        ),
    )

    private fun typesIn(vararg namespaces: String): List<String> = listedTypes.inAndUnder(namespaces.map(::QualifiedName)).map { it.qualifiedName.text }

    @Test
    fun `a type a source generator wrote is among its namespace's types`() =
        assertEquals(listOf("Shop.Billing.Invoice", "Shop.Billing.Receipt"), typesIn("Shop.Billing"))

    @Test
    fun `several namespaces have their types each in turn, by their names`() =
        assertEquals(listOf("Shop.Billing.Invoice", "Shop.Billing.Receipt", "Shop.Orders.Shipping.Parcel"), typesIn("Shop.Orders.Shipping", "Shop.Billing"))

    private fun listing(name: String, vararg types: String): NamespaceListing =
        Gson().fromJson("""{ "name": "$name", "childNamespaces": [], "types": [${types.joinToString()}] }""", NamespaceListing::class.java)

    /** A type's listing, in [file], or, when [generatedAs] names it, in the file a source generator wrote under that name. */
    private fun type(
        kind: String,
        qualifiedName: String,
        file: String? = """C:\\Shop\\${qualifiedName.substringAfterLast('.')}.cs""",
        generatedAs: String? = null,
    ): String =
        """{ "kind": "$kind", "name": "${qualifiedName.substringAfterLast('.')}", "qualifiedName": "$qualifiedName", ${file?.let { "\"file\": \"$it\"," } ?: ""} ${generatedAs?.let { "\"generator\": \"Shop.Generators.ShopGenerator\", \"hintName\": \"$it\"," } ?: ""} "line": 3, "column": 14, "nestedTypes": [] }"""
}
