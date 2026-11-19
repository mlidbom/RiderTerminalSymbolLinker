package dev.magnus.claudesessions.sessionhosts

import dev.magnus.claudesessions.ClaudeSessionTab

/**
 * A session host: a process the Claude tooling's hub starts for a [ClaudeSessionTab], outside the IDE, that owns a
 * console with the tab's shell in it, and Claude Code in the shell. The tab shows the session by running the host's
 * attach client, [attachCommand], which relays between the tab's terminal and the host's console. When the IDE exits,
 * crashes, or reloads this plugin, only the attach client ends: the session runs on, mid-turn included, and a tab that
 * runs the attach client again shows it as it is. Closing the tab ends it.
 *
 * The hub's contract, `~/.claude/dev_docs/the-hubs-local-api.md`, "Session hosts", says what it is and does.
 *
 * @property id What the hub, the attach client and Claude Code's running session name the host by.
 * @property shell The shell in the host's console, which Claude Code is typed into, or null once it has exited.
 * @property label What the plugin gave the hub to know the host by, as it gave it: see [SessionHostLabel].
 * @property attached Whether an attach client was connected to it when the hub told of it.
 * @property attachCommand The command, as its arguments, that a tab runs to show the session.
 */
data class SessionHost(val id: SessionHostId, val shell: ProcessHandle?, val label: String?, val attached: Boolean, val attachCommand: List<String>)
