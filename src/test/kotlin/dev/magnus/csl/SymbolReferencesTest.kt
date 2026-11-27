package dev.magnus.csl

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/**
 * How [SymbolReferences.find] carves the C# symbols a line names out of it: words that name a type or member, words
 * joined by dots that are the end of a symbol's qualified name, as in `ApplicationWindow.ForceToFront` and
 * `Ui.Windows.ApplicationWindow.ForceToFront`, as one symbol, and two words or more that are the end of a namespace's
 * name, as one namespace. The cases are Cloyd's, whose chat links names as this plugin does (`~/.claude`,
 * `src/Cloyd.Specifications/Links/Finding_the_symbols_a_line_names.cs`).
 */
class SymbolReferencesTest {

    private val declared = DeclaredNames(
        QualifiedNames(
            listOf(
                "Ui.Windows.ApplicationWindow", "Ui.Windows.ApplicationWindow.ForceToFront",
                "Ui.Windows.ApplicationWindow.Frame", "Ui.Windows.ApplicationWindow.Frame.Width",
                "Ui.Windows.NativeWindow", "Ui.Windows.NativeWindow.ForceToFront",
                "Ui.Windows.Logger",
                "Ui.Windows.LogEntry", "Ui.Windows.LogEntry.Info", "Ui.Windows.LogEntry.Id",
                "Ui.Windows.Point", "Ui.Windows.Point.X", "Ui.Windows.Point.Y",
                "Ui.Windows.Native.Interop.Handle",
            ).map(::QualifiedName),
        ),
        QualifiedNames(listOf("Ui", "Ui.Windows", "Ui.Windows.Native", "Ui.Windows.Native.Interop").map(::QualifiedName)),
    )

    private fun find(line: String): List<SymbolReference> = SymbolReferences.find(line, declared)

    private fun typeOrMember(range: IntRange, name: String) = SymbolReference.ToATypeOrMember(range, name)

    private fun namespace(range: IntRange, name: String) = SymbolReference.ToANamespace(range, name)

    @Test
    fun `a type's member links as one symbol`() =
        assertEquals(listOf(typeOrMember(5..34, "ApplicationWindow.ForceToFront")), find("call ApplicationWindow.ForceToFront() now"))

    @Test
    fun `a member qualified by its namespace links as one symbol`() =
        assertEquals(listOf(typeOrMember(0..40, "Ui.Windows.ApplicationWindow.ForceToFront")), find("Ui.Windows.ApplicationWindow.ForceToFront"))

    @Test
    fun `a type qualified by part of its namespace links as one symbol`() =
        assertEquals(listOf(typeOrMember(4..17, "Windows.Logger")), find("see Windows.Logger"))

    @Test
    fun `a nested type's member links as one symbol`() =
        assertEquals(listOf(typeOrMember(0..28, "ApplicationWindow.Frame.Width")), find("ApplicationWindow.Frame.Width"))

    @Test
    fun `two names joined by a dot that are no type's member link each on its own`() =
        assertEquals(listOf(typeOrMember(0..5, "Logger"), typeOrMember(7..10, "Info")), find("Logger.Info"))

    @Test
    fun `names before a type that are not its namespaces are left out of the link`() =
        assertEquals(listOf(typeOrMember(8..37, "ApplicationWindow.ForceToFront")), find("Foo.Bar.ApplicationWindow.ForceToFront"))

    @Test
    fun `a type with a member it doesn't have links as the type alone`() =
        assertEquals(listOf(typeOrMember(0..16, "ApplicationWindow")), find("ApplicationWindow.Unknown"))

    @Test
    fun `a type's name links`() = assertEquals(listOf(typeOrMember(4..20, "ApplicationWindow")), find("see ApplicationWindow here"))

    @Test
    fun `a name of two letters links on its own`() = assertEquals(listOf(typeOrMember(4..5, "Id")), find("the Id field"))

    @Test
    fun `a name of one letter doesn't link on its own`() = assertEquals(emptyList<SymbolReference>(), find("the X field"))

    @Test
    fun `a name of one letter links after its type's name`() = assertEquals(listOf(typeOrMember(0..6, "Point.X")), find("Point.X"))

    @Test
    fun `a namespace's name links as one namespace`() = assertEquals(listOf(namespace(3..12, "Ui.Windows")), find("in Ui.Windows here"))

    @Test
    fun `the end of a namespace's name links as the namespace`() = assertEquals(listOf(namespace(0..13, "Native.Interop")), find("Native.Interop"))

    @Test
    fun `a namespace with no types of its own links`() = assertEquals(listOf(namespace(0..16, "Ui.Windows.Native")), find("Ui.Windows.Native"))

    @Test
    fun `one name of a namespace alone doesn't link`() = assertEquals(emptyList<SymbolReference>(), find("the Windows here"))

    @Test
    fun `names of a namespace that leave out one between them don't link`() = assertEquals(emptyList<SymbolReference>(), find("Ui.Native"))

    @Test
    fun `prose names no symbol`() = assertEquals(emptyList<SymbolReference>(), find("just some prose here"))

    @Test
    fun `a name in a path is part of the path`() = assertEquals(emptyList<SymbolReference>(), find("src/ApplicationWindow.cs"))
}
