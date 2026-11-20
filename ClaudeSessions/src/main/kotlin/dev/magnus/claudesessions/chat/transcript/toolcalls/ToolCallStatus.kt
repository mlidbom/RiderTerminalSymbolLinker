package dev.magnus.claudesessions.chat.transcript.toolcalls

import dev.magnus.claudesessions.chat.claudecode.conversation.ToolCall
import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/**
 * How tool calls the [TranscriptPane] shows as one line went: whether the last of them still runs, and how many
 * failed. Its [suffix] ends the line, such as ` · running`, or ` · 1 failed`.
 *
 * @property ofOneCall Whether the line is of one call, whose failure is told as ` · failed`.
 */
class ToolCallStatus private constructor(val isRunning: Boolean, val failed: Int, private val ofOneCall: Boolean) {
    /** What ends the line of the calls: nothing when the last is done and none failed. */
    val suffix: String
        get() = when {
            isRunning && failed > 0 -> " · $failed failed · running"
            isRunning -> " · running"
            failed > 0 && ofOneCall -> " · failed"
            failed > 0 -> " · $failed failed"
            else -> ""
        }

    companion object {
        /** How [calls] went. */
        fun of(calls: List<ToolCall>): ToolCallStatus =
            ToolCallStatus(isRunning = calls.last().result == null, failed = calls.count { it.result?.isAnError == true }, ofOneCall = calls.size == 1)
    }
}
