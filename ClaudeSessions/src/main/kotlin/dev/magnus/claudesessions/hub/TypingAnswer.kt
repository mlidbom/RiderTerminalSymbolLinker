package dev.magnus.claudesessions.hub

import com.google.gson.JsonParser

/**
 * What came of asking the hub to type into Claude Code, or into the shell below it: typed, or not, with why. The hub
 * answers once it has typed, which may take minutes while the tooling's own typing has the session's turn.
 */
sealed interface TypingAnswer {
    /** Typed, as asked. */
    data object Typed : TypingAnswer

    /**
     * Not typed, or not finished: [why], for the user, and the hub's [reason], which the contract lists, such as
     * `inputBoxHoldsText`, or null when no hub could be asked. The hub never puts what the user wrote in [why].
     */
    data class NotTyped(val why: String, val reason: String?) : TypingAnswer

    companion object {
        /**
         * The answer the hub gave, with [status] and [body], to a request that typed or was refused: 200, or 404 and 409
         * with the hub's `{"error", "reason"}`. A 404 with no body comes from a hub older than its typing, which serves no
         * such endpoint.
         */
        fun of(status: Int, body: String, build: String): TypingAnswer = when {
            status == 200 -> Typed
            status == 404 && body.isBlank() -> NotTyped("The Claude tooling's hub of build $build doesn't type into sessions: it's older than its typing.", reason = null)
            status == 404 || status == 409 -> JsonParser.parseString(body).asJsonObject.let { NotTyped(it.get("error").asString, it.get("reason").asString) }
            else -> error("The hub of build $build answered a request to type with $status: $body")
        }
    }
}
