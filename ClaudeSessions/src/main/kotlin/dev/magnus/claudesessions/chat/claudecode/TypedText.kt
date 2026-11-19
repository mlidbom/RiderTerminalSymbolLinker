package dev.magnus.claudesessions.chat.claudecode

/** Text of a [Message] that the user typed: its lines apart by `\n`. */
data class TypedText(override val text: String) : MessagePart
