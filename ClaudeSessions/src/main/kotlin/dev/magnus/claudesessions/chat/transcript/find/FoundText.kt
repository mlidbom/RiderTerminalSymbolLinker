package dev.magnus.claudesessions.chat.transcript.find

import dev.magnus.claudesessions.chat.transcript.ItemTextPart
import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/** A place Find found what it seeks in the [TranscriptPane]: in the text [part], at [range]. */
data class FoundText(val part: ItemTextPart, val range: IntRange)
