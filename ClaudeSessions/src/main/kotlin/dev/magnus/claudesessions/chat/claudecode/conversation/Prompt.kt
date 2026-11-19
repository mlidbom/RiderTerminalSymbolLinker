package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * What the user said to Claude: the [text] of a prompt, or of a slash command, as typed, such as `/compact`. An image
 * in it is its place-holder, such as `[Image #1]`.
 */
data class Prompt(override val id: String, val text: String) : ConversationItem
