package dev.magnus.claudesessions.hub

import com.google.gson.JsonElement
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import dev.magnus.claudesessions.claudecode.ClaudeCodesStatusLine
import dev.magnus.claudesessions.claudecode.RunningClaudeCodes
import dev.magnus.claudesessions.claudecode.TurnUnderWay
import java.time.Instant

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
                name = session.get("name").stringOrNull,
                directory = session.get("directory").asString,
                sessionHost = session.get("sessionHost").stringOrNull,
                statusLine = statusLineIn(session),
                turnUnderWay = turnUnderWayIn(session),
            )

        /** What the running session [session] tells of what its Claude Code last showed under its input box, its `statusLine`. */
        internal fun statusLineIn(session: JsonObject): ClaudeCodesStatusLine? = session.objectOrNull("statusLine")?.let(::readStatusLine)

        /** What the running session [session] tells of its turn under way, its `turnUnderWay`. */
        internal fun turnUnderWayIn(session: JsonObject): TurnUnderWay? = session.objectOrNull("turnUnderWay")?.let(::readTurn)

        private fun readStatusLine(statusLine: JsonObject): ClaudeCodesStatusLine =
            ClaudeCodesStatusLine(
                model = statusLine.getAsJsonObject("model").get("displayName").asString,
                effortLevel = statusLine.get("effortLevel").stringOrNull,
                contextUsedPercentage = statusLine.objectOrNull("contextWindow")?.get("usedPercentage")?.takeUnless { it.isJsonNull }?.asInt,
                permissionMode = statusLine.objectOrNull("permissionMode")?.let { ClaudeCodesStatusLine.PermissionMode(it.get("name").stringOrNull, it.get("shownAs").asString) },
            )

        private fun readTurn(turn: JsonObject): TurnUnderWay {
            val activity = turn.getAsJsonObject("activity")
            return TurnUnderWay(
                startedAt = Instant.parse(turn.get("startedAt").asString),
                activity = activityOf(activity.get("kind").asString),
                toolBeingWritten = activity.get("toolName").stringOrNull,
                outputTokensSoFar = turn.get("outputTokensSoFar").asLong,
            )
        }

        /** The activity the hub names [kind]: one this build doesn't know is [TurnUnderWay.Activity.OTHER], as the contract has readers show it as busy. */
        private fun activityOf(kind: String): TurnUnderWay.Activity = when (kind) {
            "requesting" -> TurnUnderWay.Activity.REQUESTING
            "thinking" -> TurnUnderWay.Activity.THINKING
            "writing" -> TurnUnderWay.Activity.WRITING
            "writingAToolCall" -> TurnUnderWay.Activity.WRITING_A_TOOL_CALL
            "runningTools" -> TurnUnderWay.Activity.RUNNING_TOOLS
            else -> TurnUnderWay.Activity.OTHER
        }

        /** The string, or null for JSON's null or a field left out, as a hub older than the field leaves it. */
        private val JsonElement?.stringOrNull: String? get() = this?.takeUnless { it.isJsonNull }?.asString

        /** The object, or null for JSON's null or a field left out, as a hub older than the field leaves it. */
        private fun JsonObject.objectOrNull(field: String): JsonObject? = get(field)?.takeUnless { it.isJsonNull }?.asJsonObject
    }
}
