package dev.magnus.csl

import java.awt.Point

/**
 * What a click on a terminal link did: opened a file, showed the [LinkPicker], found nothing, or fell back to
 * Search Everywhere. The links report it to [LinkClickOutcomes], for the development observation channel
 * ([dev.magnus.csl.observation.ObservationChannel]) to tell a test what happened.
 */
sealed interface LinkClickOutcome {
    /** Opened [file] at [line] (1-based): the target the link resolved to. */
    data class Opened(val file: String, val line: Int) : LinkClickOutcome

    /**
     * Showed the [LinkPicker] titled [title], with [note] above its rows, one row per candidate, its top left corner at
     * [location] on the screen. [choose] opens a row's candidate and closes the picker, as Enter does; [close] closes it
     * without opening anything.
     */
    class PickerShown(
        val title: String,
        val note: String?,
        val location: Point,
        val rows: List<String>,
        val choose: (row: Int) -> Unit,
        val close: () -> Unit,
    ) : LinkClickOutcome

    /** Told the user, with [message], that the link's target doesn't exist. */
    data class NothingFound(val message: String) : LinkClickOutcome

    /** The MCP server couldn't be reached, so it opened Search Everywhere for [token]. */
    data class FellBackToSearchEverywhere(val token: String) : LinkClickOutcome
}

/**
 * Where links report their [LinkClickOutcome]. Nothing listens unless the development observation channel runs,
 * which it does only in the sandbox Rider, so for users a report goes nowhere.
 */
object LinkClickOutcomes {
    @Volatile
    var observer: ((LinkClickOutcome) -> Unit)? = null

    fun report(outcome: LinkClickOutcome) {
        observer?.invoke(outcome)
    }
}
