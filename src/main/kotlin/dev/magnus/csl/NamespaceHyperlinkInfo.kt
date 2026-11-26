package dev.magnus.csl

import com.intellij.openapi.project.Project
import com.intellij.ui.awt.RelativePoint

/**
 * A link to a namespace, by [name] as the terminal's text writes it: its name, or the end of it from two of its names
 * on, such as `Geometry.Shapes` or `Shapes.Drawing`. A click always shows a picker of the types in each namespace the
 * name names and in the namespaces under them, as [TypesOfNamespaces.inAndUnder] orders them, even of one type alone,
 * so that Magnus sees he clicked a namespace (2026-11-26). Above them, the picker says so ([NOTE]).
 */
class NamespaceHyperlinkInfo(name: String) : HyperlinkInfoResolvedByTheMcp(name) {

    override fun resolve(project: Project, solutionName: String?): List<SymbolHit>? =
        DotNetAnalysisAndRefactoringMcp.typesInAndUnder(SymbolIndex.getInstance(project).declared.namespacesNamedBy(name), solutionName)

    override fun leadTo(project: Project, declarations: List<SymbolHit>, clickedAt: RelativePoint?) {
        if (declarations.isEmpty()) tellNothingFound(project) else showPicker(project, "Namespace \"$name\"", NOTE, declarations, clickedAt)
    }

    override val nothingFoundMessage: String get() = "No types in namespace \"$name\" found"

    companion object {
        /** What the picker says above the types. */
        const val NOTE = "You clicked a namespace. These are the types in it and in the namespaces under it: choose one to open it."
    }
}
