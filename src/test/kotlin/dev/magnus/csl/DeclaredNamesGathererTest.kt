package dev.magnus.csl

import com.google.gson.Gson
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/** Which names a namespace the .NET Analysis&Refactoring MCP lists contributes to a solution's [DeclaredNames]. */
class DeclaredNamesGathererTest {

    @Test
    fun `a type gives its qualified name, and its members theirs after it`() {
        val names = gathered(
            """
            { "name": "Geometry.Shapes", "childNamespaces": [], "types": [
              { "kind": "class", "name": "Circle", "qualifiedName": "Geometry.Shapes.Circle", "members": [
                  { "access": "public", "kind": "property", "name": "Radius", "signature": "property Radius : double" },
                  { "access": "public", "kind": "method", "name": "Area", "signature": "method Area() : double" },
                  { "access": "private", "kind": "field", "name": "cache", "signature": "field cache : double" },
                  { "access": "public", "kind": "event", "name": "Resized", "signature": "event Resized : EventHandler" }
                ], "nestedTypes": [] }
            ] }
            """,
        )

        assertEquals(
            setOf("Geometry.Shapes.Circle", "Geometry.Shapes.Circle.Radius", "Geometry.Shapes.Circle.Area", "Geometry.Shapes.Circle.cache", "Geometry.Shapes.Circle.Resized"),
            names.typesAndMembers.texts,
        )
    }

    @Test
    fun `a namespace with types gives its name and those of the namespaces around it`() =
        assertEquals(setOf("Geometry", "Geometry.Shapes"), gathered("""{ "name": "Geometry.Shapes", "childNamespaces": [], "types": [ $CIRCLE ] }""").namespaces.texts)

    @Test
    fun `a namespace with no types gives no name`() =
        assertEquals(emptySet<String>(), gathered("""{ "name": "Geometry", "childNamespaces": ["Geometry.Shapes"], "types": [] }""").namespaces.texts)

    @Test
    fun `the global namespace's types give their names, and it none`() {
        val names = gathered("""{ "name": "", "childNamespaces": [], "types": [ { "kind": "class", "name": "Ruler", "qualifiedName": "Ruler", "members": [], "nestedTypes": [] } ] }""")

        assertEquals(setOf("Ruler"), names.typesAndMembers.texts)
        assertEquals(emptySet<String>(), names.namespaces.texts)
    }

    @Test
    fun `an extension block's members, properties included, are members of the class that declares the block`() {
        val names = gathered(
            """
            { "name": "Geometry.Shapes", "childNamespaces": [], "types": [
              { "kind": "class", "name": "ShapeExtensions", "qualifiedName": "Geometry.Shapes.ShapeExtensions", "members": [
                  { "access": "public", "kind": "method", "name": "TotalArea", "signature": "method TotalArea(shapes:IEnumerable<IShape>) : double" },
                  { "kind": "extension", "signature": "extension(circle:Circle)", "receiver": { "name": "circle", "type": "Circle", "typeName": "Circle" },
                    "members": [
                      { "access": "public", "kind": "property", "name": "Diameter", "signature": "property Diameter : double" },
                      { "access": "public", "kind": "method", "name": "Grown", "signature": "method Grown(by:double) : Circle" }
                    ] }
                ], "nestedTypes": [] }
            ] }
            """,
        )

        assertEquals(
            setOf("Geometry.Shapes.ShapeExtensions", "Geometry.Shapes.ShapeExtensions.TotalArea", "Geometry.Shapes.ShapeExtensions.Diameter", "Geometry.Shapes.ShapeExtensions.Grown"),
            names.typesAndMembers.texts,
        )
    }

    @Test
    fun `a nested type gives its qualified name and its members theirs`() {
        val names = gathered(
            """
            { "name": "Geometry.Shapes", "childNamespaces": [], "types": [
              { "kind": "class", "name": "Drawing", "qualifiedName": "Geometry.Shapes.Drawing", "members": [], "nestedTypes": [
                  { "kind": "record", "name": "Stroke", "qualifiedName": "Geometry.Shapes.Drawing.Stroke", "members": [
                      { "access": "public", "kind": "property", "name": "Width", "signature": "property Width : double" }
                    ], "nestedTypes": [] }
                ] }
            ] }
            """,
        )

        assertEquals(setOf("Geometry.Shapes.Drawing", "Geometry.Shapes.Drawing.Stroke", "Geometry.Shapes.Drawing.Stroke.Width"), names.typesAndMembers.texts)
    }

    @Test
    fun `constructors, indexers and operators give no names, since code doesn't call them by those`() {
        val names = gathered(
            """
            { "name": "Geometry", "childNamespaces": [], "types": [
              { "kind": "struct", "name": "Vector", "qualifiedName": "Geometry.Vector", "members": [
                  { "access": "public", "kind": "constructor", "name": "Vector", "signature": "constructor .ctor(x:double)" },
                  { "access": "public", "kind": "indexer", "name": "this", "signature": "indexer this[index:int] : double" },
                  { "access": "public", "kind": "operator", "name": "op_Addition", "signature": "operator op_Addition(a:Vector, b:Vector) : Vector" }
                ], "nestedTypes": [] }
            ] }
            """,
        )

        assertEquals(setOf("Geometry.Vector"), names.typesAndMembers.texts)
    }

    private val QualifiedNames.texts: Set<String> get() = all.map { it.text }.toSet()

    private fun gathered(namespace: String): DeclaredNames =
        DeclaredNamesGatherer().apply { add(Gson().fromJson(namespace.trimIndent(), NamespaceListing::class.java)) }.gathered()

    private companion object {
        const val CIRCLE = """{ "kind": "class", "name": "Circle", "qualifiedName": "Geometry.Shapes.Circle", "members": [], "nestedTypes": [] }"""
    }
}
