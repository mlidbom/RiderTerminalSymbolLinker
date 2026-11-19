package dev.magnus.claudesessions.hub

/**
 * The version of the hub's local API that this build of the plugin is written against: what the [HubAnnouncement] and
 * the header [HEADER] of every answer say. The hub raises it only for a change a reader of the earlier one would get
 * wrong; a change that only adds, a field, an endpoint, an event or a value, keeps it, and the plugin ignores what it
 * doesn't know. See `~/.claude/dev_docs/the-hubs-local-api.md`, the contract.
 */
object HubProtocol {
    const val VERSION = 1

    /** The header of every answer of the hub's that says its protocol's version. */
    const val HEADER = "Claude-Tooling-Protocol"
}
