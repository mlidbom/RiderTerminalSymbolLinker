package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * A tool Claude ran, such as `Bash` or `Read`: its [input], and its [result] once it has one.
 *
 * @property id The tool call's id, as Claude Code records it, the same before and after its result came.
 * @property tool The tool's name, as Claude Code names it: `Bash`, `Read`, `Edit`, or an MCP tool's long name.
 * @property input What Claude gave the tool, field by field, in the order Claude gave them.
 */
data class ToolCall(override val id: String, val tool: String, val input: List<ToolInputField>, val result: ToolResult?) : ConversationItem {
    /** The input field named [name], or null when Claude gave none. */
    fun inputField(name: String): ToolInputField? = input.firstOrNull { it.name == name }
}
