package dev.magnus.claudesessions.chat.claudecode

/**
 * A message the user sends Claude Code from the chat: its [parts], in their order, each as it came into the chat's
 * input box. What the user typed, what they pasted, and the images they pasted are handed on apart, so that Claude Code
 * gets each as it would in its own input box: it marks pasted text as pasted, for one.
 */
data class Message(val parts: List<MessagePart>) {
    /** The message's text, as the user sees it in the input box: each image as its place-holder. */
    val text: String get() = parts.joinToString("") { it.text }

    /** Whether the message runs a slash command, as Claude Code takes one: it starts with `/`. */
    val isASlashCommand: Boolean get() = text.startsWith("/")
}
