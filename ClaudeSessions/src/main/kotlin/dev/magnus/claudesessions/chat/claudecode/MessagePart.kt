package dev.magnus.claudesessions.chat.claudecode

/** A part of a [Message]: [TypedText], [PastedText] or a [PastedImage]. */
sealed interface MessagePart {
    /** The part's text, as the input box shows it. */
    val text: String
}
