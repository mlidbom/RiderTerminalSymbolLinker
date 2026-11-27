package dev.magnus.csl

import com.intellij.openapi.project.Project
import com.intellij.ui.awt.RelativePoint

/**
 * A link to a type or member, by [name] as the terminal's text writes it: its name, or the end of its qualified name,
 * such as `Circle.Area` or `Geometry.Shapes.Circle.Area`. A click asks the .NET Analysis&Refactoring MCP for its
 * declarations, exactly by that name:
 *  - one declaration: opens it, with the caret on its name;
 *  - several: a picker of them, at the click, such as a method's overloads, or two types of one name;
 *  - none: a brief notice;
 *  - MCP unreachable: Search Everywhere, see [HyperlinkInfoResolvedByTheMcp].
 *
 * In the picker, a plain click (or Enter) opens the declaration and closes the list, as usual. A Ctrl-click (Cmd-click
 * on macOS) instead opens it in the background and leaves the list open, so several can be opened in turn.
 */
class SymbolHyperlinkInfo(name: String) : HyperlinkInfoResolvedByTheMcp(name) {

    /** The declarations [name] names: those whose qualified names end with it. */
    override fun resolve(project: Project, solutionName: String?): List<SymbolHit>? =
        DotNetAnalysisAndRefactoringMcp.goToDefinition(name, solutionName)

    override fun leadTo(project: Project, declarations: List<SymbolHit>, clickedAt: RelativePoint?) {
        when (declarations.size) {
            0 -> tellNothingFound(project)
            1 -> open(project, declarations.single())
            else -> showPicker(project, "Symbols matching \"$name\"", note = null, declarations, clickedAt)
        }
    }

    override val nothingFoundMessage: String get() = "No symbol \"$name\" found"
}
