package dev.magnus.csl

import com.google.gson.Gson
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/**
 * Which types a click on a link to a namespace lists ([TypesOfNamespaces.inAndUnder]): its own first, then those of the
 * namespaces under it, nested types included and members not, as Magnus agreed (2026-11-26). The cases are Cloyd's
 * (`~/.claude`, `src/Cloyd.Specifications/Links/Finding_the_types_of_a_namespace.cs`).
 */
class TypesOfNamespacesTest {

    private val types = TypesOfNamespaces(
        listOf(
            listing("Shop", listOf("Shop.Orders", "Shop.Billing")),
            listing(
                "Shop.Orders", listOf("Shop.Orders.Shipping"),
                type("class", "Shop.Orders.Order", type("class", "Shop.Orders.Order.Line")),
                type("enum", "Shop.Orders.Status"),
            ),
            listing("Shop.Orders.Shipping", emptyList(), type("class", "Shop.Orders.Shipping.Parcel")),
            listing("Shop.Billing", emptyList(), type("class", "Shop.Billing.Invoice"), type("class", "Shop.Billing.Generated", file = null)),
        ),
    )

    private fun typesIn(vararg namespaces: String): List<String> = types.inAndUnder(namespaces.map(::QualifiedName)).map { it.qualifiedName.text }

    @Test
    fun `a namespace has its own types first, then those of the namespaces under it, and not their members`() =
        assertEquals(listOf("Shop.Orders.Order", "Shop.Orders.Order.Line", "Shop.Orders.Status", "Shop.Orders.Shipping.Parcel"), typesIn("Shop.Orders"))

    @Test
    fun `a namespace with no types of its own has those of the namespaces under it, a source generator's left out`() =
        assertEquals(
            listOf("Shop.Billing.Invoice", "Shop.Orders.Order", "Shop.Orders.Order.Line", "Shop.Orders.Shipping.Parcel", "Shop.Orders.Status"),
            typesIn("Shop"),
        )

    @Test
    fun `several namespaces have their types each in turn, by their names`() =
        assertEquals(listOf("Shop.Billing.Invoice", "Shop.Orders.Shipping.Parcel"), typesIn("Shop.Orders.Shipping", "Shop.Billing"))

    private fun listing(name: String, childNamespaces: List<String>, vararg types: String): NamespaceListing =
        Gson().fromJson("""{ "name": "$name", "childNamespaces": [${childNamespaces.joinToString { "\"$it\"" }}], "types": [${types.joinToString()}] }""", NamespaceListing::class.java)

    private fun type(kind: String, qualifiedName: String, vararg nestedTypes: String, file: String? = """C:\\Shop\\${qualifiedName.substringAfterLast('.')}.cs"""): String =
        """{ "kind": "$kind", "name": "${qualifiedName.substringAfterLast('.')}", "qualifiedName": "$qualifiedName", ${file?.let { "\"file\": \"$it\", \"line\": 3, \"column\": 14," } ?: ""} "nestedTypes": [${nestedTypes.joinToString()}] }"""
}
