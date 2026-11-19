package dev.magnus.claudesessions.hub

/**
 * Reads a stream of Server-Sent Events, the format in which the hub streams what it follows, line by line: each event
 * its `event:` and `data:` lines, ended by a blank line. A line that starts with `:` is a comment, such as the hub's
 * `: keep-alive`, and fields other than those two are left out, as the plugin needs neither. As the HTML standard's
 * "Server-sent events" reads them.
 */
class ServerSentEvents {
    private var name: String? = null
    private val data = StringBuilder()
    private var hasData = false

    /** Reads the stream's next [line], without its line break, and returns the event it ends, if it ends one. */
    fun read(line: String): ServerSentEvent? {
        if (line.isEmpty()) return endTheEvent()
        if (line.startsWith(":")) return null
        val field = line.substringBefore(':')
        val value = line.substringAfter(':', "").removePrefix(" ")
        when (field) {
            "event" -> name = value
            "data" -> {
                if (hasData) data.append('\n')
                data.append(value)
                hasData = true
            }
        }
        return null
    }

    /** The event the blank line just read ends: none for one without data, as the standard says. */
    private fun endTheEvent(): ServerSentEvent? {
        val event = if (hasData) ServerSentEvent(name ?: "message", data.toString()) else null
        name = null
        data.clear()
        hasData = false
        return event
    }
}
