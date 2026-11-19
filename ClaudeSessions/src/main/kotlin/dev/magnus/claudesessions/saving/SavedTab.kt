package dev.magnus.claudesessions.saving

import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.claudecode.ClaudeSession
import dev.magnus.claudesessions.sessionhosts.SessionHost
import dev.magnus.claudesessions.sessionhosts.SessionHostId

/**
 * A [ClaudeSessionTab] as the [ClaudeSessionKeeper] saves it, for it to come back as the IDE starts: the session it runs,
 * or ran last, and its [SessionHost], which it attaches to again if the host still runs. A session whose host is gone,
 * as after a reboot, is resumed in a new one.
 *
 * @property session The session, or null while Claude Code hasn't recorded the one it started.
 * @property sessionHost The tab's host, or null for a tab that has none, as a terminal tab the view didn't open.
 */
data class SavedTab(val session: ClaudeSession?, val sessionHost: SessionHostId?) {
    init {
        require(session != null || sessionHost != null) { "A tab with neither a session nor a session host has nothing to come back with." }
    }
}
