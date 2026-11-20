package dev.magnus.claudesessions.chat.transcript.find

import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/** Where a text the [TranscriptPane] shows holds what Find seeks, at [range], and whether it's the place the user is at, [isCurrent]. */
data class Highlight(val range: IntRange, val isCurrent: Boolean)
