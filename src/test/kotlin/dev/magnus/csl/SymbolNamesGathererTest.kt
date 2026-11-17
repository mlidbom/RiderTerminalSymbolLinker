package dev.magnus.csl

import com.google.gson.Gson
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/** Which names a type the .NET Analysis&Refactoring MCP lists contributes to a solution's [SymbolNames]. */
class SymbolNamesGathererTest {

    @Test
    fun `a type gives its name, and its members their names and their pairs with the type's name`() {
        val names = gathered(
            """
            { "kind": "class", "name": "Circle", "members": [
                { "access": "public", "kind": "property", "name": "Radius", "signature": "property Radius : double" },
                { "access": "public", "kind": "method", "name": "Area", "signature": "method Area() : double" },
                { "access": "private", "kind": "field", "name": "cache", "signature": "field cache : double" },
                { "access": "public", "kind": "event", "name": "Resized", "signature": "event Resized : EventHandler" }
              ], "nestedTypes": [] }
            """,
        )

        assertEquals(setOf("Circle", "Radius", "Area", "cache", "Resized"), names.short)
        assertEquals(setOf("Circle.Radius", "Circle.Area", "Circle.cache", "Circle.Resized"), names.combined)
    }

    @Test
    fun `an extension block's members, properties included, are members of the class that declares the block`() {
        val names = gathered(
            """
            { "kind": "class", "name": "ShapeExtensions", "members": [
                { "access": "public", "kind": "method", "name": "TotalArea", "signature": "method TotalArea(shapes:IEnumerable<IShape>) : double" },
                { "kind": "extension", "signature": "extension(circle:Circle)", "receiver": { "name": "circle", "type": "Circle", "typeName": "Circle" },
                  "members": [
                    { "access": "public", "kind": "property", "name": "Diameter", "signature": "property Diameter : double" },
                    { "access": "public", "kind": "method", "name": "Grown", "signature": "method Grown(by:double) : Circle" }
                  ] }
              ], "nestedTypes": [] }
            """,
        )

        assertEquals(setOf("ShapeExtensions", "TotalArea", "Diameter", "Grown"), names.short)
        assertEquals(setOf("ShapeExtensions.TotalArea", "ShapeExtensions.Diameter", "ShapeExtensions.Grown"), names.combined)
    }

    @Test
    fun `a nested type is a member of the type it's nested in, and gives its own members`() {
        val names = gathered(
            """
            { "kind": "class", "name": "Drawing", "members": [], "nestedTypes": [
                { "kind": "record", "name": "Stroke", "members": [
                    { "access": "public", "kind": "property", "name": "Width", "signature": "property Width : double" }
                  ], "nestedTypes": [] }
              ] }
            """,
        )

        assertEquals(setOf("Drawing", "Stroke", "Width"), names.short)
        assertEquals(setOf("Drawing.Stroke", "Stroke.Width"), names.combined)
    }

    @Test
    fun `constructors, indexers and operators give no names, since code doesn't call them by those`() {
        val names = gathered(
            """
            { "kind": "struct", "name": "Vector", "members": [
                { "access": "public", "kind": "constructor", "name": "Vector", "signature": "constructor .ctor(x:double)" },
                { "access": "public", "kind": "indexer", "name": "this", "signature": "indexer this[index:int] : double" },
                { "access": "public", "kind": "operator", "name": "op_Addition", "signature": "operator op_Addition(a:Vector, b:Vector) : Vector" }
              ], "nestedTypes": [] }
            """,
        )

        assertEquals(setOf("Vector"), names.short)
        assertEquals(emptySet<String>(), names.combined)
    }

    private fun gathered(type: String): SymbolNames =
        SymbolNamesGatherer().apply { add(Gson().fromJson(type.trimIndent(), TypeListing::class.java)) }.gathered()
}
