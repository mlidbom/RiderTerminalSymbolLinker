package dev.magnus.claudesessions.sessionhosts

/** The id of a [SessionHost], as the hub gives it: what its attach client, and each running session in it, name it by. */
@JvmInline
value class SessionHostId(val value: String)
