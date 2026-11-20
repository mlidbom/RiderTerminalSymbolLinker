package dev.magnus.claudesessions.chat.transcript.links

import androidx.compose.runtime.mutableStateMapOf
import com.intellij.execution.filters.ConsoleFilterProvider
import com.intellij.execution.filters.Filter
import com.intellij.openapi.application.smartReadAction
import com.intellij.openapi.project.Project
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/**
 * The links the console filters find in the texts the [TranscriptPane] shows, as in a terminal: in Rider the symbol
 * linker's, and in every IDE the platform's own, such as those of file paths. The filters run in the background over
 * each item's texts as the item comes or changes, and each text a filter links is a link wherever it shows in that item.
 *
 * Its state is Compose's, so that the transcript's texts show each link once it's found.
 *
 * Its methods run on the EDT.
 */
class TranscriptLinks(private val project: Project, private val scope: CoroutineScope) {
    /**
     * The console filters, each run over the text by itself, as the terminal runs them: a filter's result ends the
     * filtering of its line unless it says otherwise, so a `CompositeFilter` of them all would let the symbol linker's
     * symbol links hide its file links on the same line.
     */
    private val filters = ConsoleFilterProvider.FILTER_PROVIDERS.extensionList.flatMap { it.getDefaultFilters(project).asList() }

    private val found = mutableStateMapOf<String, List<TranscriptLink>>()

    /** What the filters are finding links in, by item: the texts, and the job that finds them. */
    private val finding = mutableMapOf<String, Pair<List<String>, Job>>()

    /** The links found in the texts of the item [item], by its id. */
    fun of(item: String): List<TranscriptLink> = found[item].orEmpty()

    /**
     * Has the filters find the links in [texts], the texts of each item by its id, in the background: anew for each
     * item whose texts changed since. Forgets the links of every other item.
     */
    fun findIn(texts: Map<String, List<String>>) {
        (finding.keys - texts.keys).forEach(::forget)
        for ((item, itsTexts) in texts) {
            if (finding[item]?.first == itsTexts) continue
            forget(item)
            finding[item] = itsTexts to scope.launch {
                val links = withContext(Dispatchers.Default) { smartReadAction(project) { linksIn(itsTexts) } }
                found[item] = links
            }
        }
    }

    private fun forget(item: String) {
        finding.remove(item)?.second?.cancel()
        found.remove(item)
    }

    /** The links every filter finds in each line of [texts], as if each were a console's only line. */
    private fun linksIn(texts: List<String>): List<TranscriptLink> =
        texts.flatMap { it.lines() }.filter(String::isNotBlank).flatMap { line -> filters.flatMap { filter -> linksIn(line, filter) } }

    private fun linksIn(line: String, filter: Filter): List<TranscriptLink> {
        val asInAConsole = "$line\n"
        return filter.applyFilter(asInAConsole, asInAConsole.length)?.resultItems.orEmpty().mapNotNull { result ->
            val info = result.hyperlinkInfo ?: return@mapNotNull null
            val end = result.highlightEndOffset.coerceAtMost(line.length)
            if (result.highlightStartOffset >= end) null else TranscriptLink(line.substring(result.highlightStartOffset, end), info)
        }
    }
}
