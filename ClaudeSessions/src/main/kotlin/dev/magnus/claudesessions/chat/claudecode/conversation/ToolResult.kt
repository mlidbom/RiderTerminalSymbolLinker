package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * What a [ToolCall] gave back to Claude.
 *
 * @property text The result's text, each image in it as a place-holder.
 * @property isAnError Whether the tool failed, or the call was refused, and [text] says why.
 */
data class ToolResult(val text: String, val isAnError: Boolean)
