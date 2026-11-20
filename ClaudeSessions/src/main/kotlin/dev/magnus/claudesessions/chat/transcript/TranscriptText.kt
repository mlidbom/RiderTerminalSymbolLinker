package dev.magnus.claudesessions.chat.transcript

/** A text the [TranscriptPane] shows, [text], and which of an item's texts it is, [part]. */
data class TranscriptText(val part: ItemTextPart, val text: String)
