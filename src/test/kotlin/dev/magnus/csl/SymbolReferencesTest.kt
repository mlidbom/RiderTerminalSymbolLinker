package dev.magnus.csl

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.DynamicTest.dynamicTest
import org.junit.jupiter.api.TestFactory

/**
 * How [SymbolReferences.find] carves the C# symbols a line names out of it: words that name a type or member, words
 * joined by dots that are the end of a symbol's qualified name, as in `ApplicationWindow.ForceToFront` and
 * `Ui.Windows.ApplicationWindow.ForceToFront`, as one symbol, and two words or more that are the end of a namespace's
 * name, as one namespace. The cases are shared with Cloyd's specifications ([CasesSharedWithCloyd]), each named as a
 * specification.
 */
class SymbolReferencesTest {

    private val shared = CasesSharedWithCloyd.read<TheSymbolsALineNames>("the-symbols-a-line-names.json")

    private val declared = NamespaceOfTheCases.declaredNamesIn(shared.declarations)

    @TestFactory
    fun `the cases shared with Cloyd`(): List<DynamicTest> = shared.cases.map { line ->
        dynamicTest(line.case) { assertEquals(line.links.map { it.asASymbolReference }, SymbolReferences.find(line.line, declared)) }
    }
}
