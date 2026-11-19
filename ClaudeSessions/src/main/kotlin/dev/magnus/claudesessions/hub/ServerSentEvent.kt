package dev.magnus.claudesessions.hub

/** An event of a stream of Server-Sent Events: its [name], and its [data], the lines of its `data:` fields. */
data class ServerSentEvent(val name: String, val data: String)
