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

    /**
     * The declarations [name] names. For a dotted name the MCP gives none of, those of the name's last word whose
     * qualified names end with all of [name]: no more than the MCP should have given.
     */
    override fun resolve(project: Project, solutionName: String?): List<SymbolHit>? {
        val declarations = DotNetAnalysisAndRefactoringMcp.goToDefinition(name, solutionName)
        if (declarations != null && declarations.isEmpty() && '.' in name) {
            //bug: the MCP's go_to_definition finds no member of a C# 14 extension block by the name of the class that
            //   declares the block, such as CircleExtensions.Diameter, though that is the end of its qualified name
            //   (told royder-agent-rep, 2026-11-26). Once it does, this second call can go.
            return DotNetAnalysisAndRefactoringMcp.goToDefinition(name.substringAfterLast('.'), solutionName)
                ?.filter { it.qualifiedName.endsWith(name) }
        }
        return declarations
    }

    override fun leadTo(project: Project, declarations: List<SymbolHit>, clickedAt: RelativePoint?) {
        when (declarations.size) {
            0 -> tellNothingFound(project)
            1 -> open(project, declarations.single())
            else -> showPicker(project, "Symbols matching \"$name\"", note = null, declarations, clickedAt)
        }
    }

    override val nothingFoundMessage: String get() = "No symbol \"$name\" found"
}
