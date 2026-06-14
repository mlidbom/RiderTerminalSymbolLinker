package dev.magnus.csl

import com.intellij.execution.filters.ConsoleFilterProvider
import com.intellij.execution.filters.Filter
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.util.Disposer

/**
 * Forces the reworked terminal to re-run its console filters over output already on screen, so symbol
 * names printed *before* the [SymbolIndex] knew them underline once it does.
 *
 * Filters normally run once, as each line is printed — there's no public "re-highlight" call. But the
 * terminal recomputes its whole filter set (and re-scans the visible buffer) whenever the
 * `com.intellij.consoleFilterProvider` extension point changes: its filter pipeline registers a change
 * listener on [ConsoleFilterProvider.FILTER_PROVIDERS]. So we nudge that EP — register then immediately
 * unregister a no-op provider. Each change fires the listener; the terminal rebuilds its filters,
 * including our [SymbolLinkFilter] (which reads the live index), and re-evaluates existing lines against
 * the now-current index. Net EP state is unchanged.
 *
 * Caveat: the nudge is global — every console/terminal listening to the EP recomputes its filters. That
 * is benign (filters just rebuild) but it is a side effect, not a targeted single-terminal refresh.
 */
object TerminalLinks {
    fun rehighlightExistingOutput() {
        ApplicationManager.getApplication().invokeLater {
            val point = ConsoleFilterProvider.FILTER_PROVIDERS.point
            val disposable = Disposer.newDisposable("csl-terminal-rehighlight")
            try {
                point.registerExtension(ConsoleFilterProvider { emptyArray<Filter>() }, disposable)
            } finally {
                Disposer.dispose(disposable) // unregister -> second EP change -> clean final state
            }
        }
    }
}
