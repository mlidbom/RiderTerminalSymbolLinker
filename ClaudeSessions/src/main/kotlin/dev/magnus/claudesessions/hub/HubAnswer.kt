package dev.magnus.claudesessions.hub

/** What a hub answered a request of its local API, through [RequestsToTheHub], or why none did. */
internal sealed interface HubAnswer {
    /** The hub of [build] answered with [status] and [body]. */
    data class Answered(val status: Int, val body: String, val build: String) : HubAnswer

    /** No hub answered, or none this build can ask: [why], for the user. */
    data class NoHubAnswered(val why: String) : HubAnswer
}
