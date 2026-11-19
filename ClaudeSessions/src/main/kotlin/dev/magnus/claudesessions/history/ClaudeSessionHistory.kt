package dev.magnus.claudesessions.history

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.diagnostic.thisLogger
import com.intellij.openapi.project.Project
import com.intellij.openapi.project.ProjectManager
import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.ClaudeSessionsView
import dev.magnus.claudesessions.api.AnotherIde
import dev.magnus.claudesessions.claudecode.ClaudeCodeTranscripts
import dev.magnus.claudesessions.claudecode.ClaudeSessionId
import dev.magnus.claudesessions.hub.ClaudeToolingHub
import java.nio.file.Path

/**
 * The sessions Claude Code has transcripts of, most recently active first, each with where it is now: what the
 * [ClaudeSessionHistoryPicker] lists, as it was when the picker opened. Left out are the sessions with no conversation,
 * and those `claude -p` ran: a tool's single prompts.
 *
 * @property worktrees The folders its scopes cover for the project the picker opened in.
 */
class ClaudeSessionHistory private constructor(val entries: List<ClaudeSessionHistoryEntry>, val worktrees: ProjectWorktrees) {
    /** The entries [scope] covers that match [filter], most recently active first. */
    fun listed(scope: ClaudeSessionHistoryScope, filter: String): List<ClaudeSessionHistoryEntry> =
        entries.filter { worktrees.cover(scope, it.session.workingDirectory) && it.matches(filter) }

    companion object {
        /**
         * Reads the history for [project]: the transcripts, the Claude Codes that run, the other IDEs' tabs and the
         * project's worktrees as it runs, and this IDE's tabs on the EDT, which it waits for. So not on the EDT.
         */
        fun readFor(project: Project): ClaudeSessionHistory {
            val started = System.currentTimeMillis()
            val summaries = ClaudeCodeTranscripts.all().mapNotNull { it.summary() }.filter { !it.wasRunByATool }
            val running = ClaudeToolingHub.getInstance().runningClaudeCodes.value.all.associate { it.session.id to it.process.pid() }
            val inTheOtherIdes = AnotherIde.all().flatMap { ide -> ide.sessionsInItsTabs().map { it.session to WhereTheSessionIs.RunningInAnotherIde(ide, it.process) } }.toMap()
            val worktrees = ProjectWorktrees.of(Path.of(project.basePath!!))
            var tabsHere: List<TabHere> = emptyList()
            ApplicationManager.getApplication().invokeAndWait {
                tabsHere = ProjectManager.getInstance().openProjects.map(ClaudeSessionsView::getInstance).flatMap { view ->
                    view.tabs.map { TabHere(view, it, it.claudeCode?.session?.id, it.lookAtTheSession()?.id) }
                }
            }

            fun whereIs(session: ClaudeSessionId): WhereTheSessionIs =
                tabsHere.firstOrNull { it.running == session }?.let { WhereTheSessionIs.RunningInATab(it.view, it.tab) }
                    ?: inTheOtherIdes[session]
                    ?: running[session]?.let(WhereTheSessionIs::RunningOutsideClaudeSessions)
                    ?: tabsHere.firstOrNull { it.ranLast == session }?.let { WhereTheSessionIs.ExitedInATab(it.view, it.tab) }
                    ?: WhereTheSessionIs.NotRunning

            val entries = summaries.sortedByDescending { it.lastActive }.map { ClaudeSessionHistoryEntry(it, whereIs(it.session.id)) }
            thisLogger().info("Read the session history for ${project.name}: ${entries.size} sessions, in ${System.currentTimeMillis() - started} ms")
            return ClaudeSessionHistory(entries, worktrees)
        }
    }

    /** A tab of this IDE, with the session its Claude Code runs, if one does, and the one it ran last. */
    private class TabHere(val view: ClaudeSessionsView, val tab: ClaudeSessionTab, val running: ClaudeSessionId?, val ranLast: ClaudeSessionId?)
}
