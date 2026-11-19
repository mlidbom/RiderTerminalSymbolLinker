package dev.magnus.claudesessions.chat.claudecode

/**
 * Text of a [Message] that the user pasted into the input box, with whatever they changed in it since: its lines apart
 * by `\n`. Claude Code marks text pasted into its own box as pasted, and tells the model that it may hold instructions
 * the user didn't write.
 */
data class PastedText(override val text: String) : MessagePart
