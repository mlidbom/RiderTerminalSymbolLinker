package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * A field of what Claude gave a [ToolCall], such as `Bash`'s `command`.
 *
 * @property value The field's value: a text as it is, anything else, such as a list or a number, as JSON.
 */
data class ToolInputField(val name: String, val value: String)
