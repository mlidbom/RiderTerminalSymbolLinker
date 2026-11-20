package dev.magnus.claudesessions.chat.transcript.links

import com.intellij.execution.filters.HyperlinkInfo
import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/** A link a console filter found in a text the [TranscriptPane] shows: the [text] it covers, and where it leads, [info]. */
class TranscriptLink(val text: String, val info: HyperlinkInfo)
