package dev.magnus.claudesessions.hub

import com.google.gson.JsonObject
import com.google.gson.JsonParser
import dev.magnus.claudesessions.claudecode.RunningClaudeCodes

/**
 * The hub's answer of the sessions that run, `{"runningSessions": [...]}`, as `GET /running-sessions` answers it and
 * each `running-sessions` event of its stream says it: every Claude Code that runs, by the records Claude Code keeps of
 * them, with the session each runs now. Only the fields the plugin uses are read; the hub's fields to come are ignored.
 */
class RunningSessionsAnswer private constructor(val runningSessions: List<RunningSessionInTheAnswer>) {
    /** The Claude Codes the answer tells of that still run, with their sessions. */
    fun runningClaudeCodes(): RunningClaudeCodes = RunningClaudeCodes(runningSessions.mapNotNull { it.asRunningClaudeCode() })

    companion object {
        /** The answer the hub's [json] says. */
        fun parse(json: String): RunningSessionsAnswer =
            RunningSessionsAnswer(JsonParser.parseString(json).asJsonObject.getAsJsonArray("runningSessions").map { readSession(it.asJsonObject) })

        private fun readSession(session: JsonObject): RunningSessionInTheAnswer =
            RunningSessionInTheAnswer(
                processId = session.get("processId").asLong,
                sessionId = session.get("sessionId").asString,
                status = session.get("status").asString,
                name = session.get("name").takeUnless { it.isJsonNull }?.asString,
                directory = session.get("directory").asString,
                sessionHost = session.get("sessionHost")?.takeUnless { it.isJsonNull }?.asString,
            )
    }
}
