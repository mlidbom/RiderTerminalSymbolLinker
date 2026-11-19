package dev.magnus.claudesessions.hub

/** What came of asking the hub to start, list or end [SessionHosts]: done, with what it gave, or not, with why. */
sealed interface SessionHostsAnswer<out T> {
    /** Done, as asked, with [result]. */
    data class Done<T>(val result: T) : SessionHostsAnswer<T>

    /** Not done: [why], for the user. */
    data class NotDone(val why: String) : SessionHostsAnswer<Nothing>
}
