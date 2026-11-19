package dev.magnus.claudesessions.claudecode

import dev.magnus.claudesessions.sessionhosts.SessionHost
import dev.magnus.claudesessions.sessionhosts.SessionHostId

/**
 * The Claude Codes that run on the machine, wherever they run, each with the session it runs, as the Claude tooling's
 * hub last told of them: the hub reads the records Claude Code keeps of its running sessions.
 *
 * Only those whose processes still run are [all]: until the next hub tells of them, as while a publish replaces the
 * hub with a new build's, which takes a few tenths of a second, or after the hub crashed, a Claude Code it told of may
 * exit.
 */
class RunningClaudeCodes(private val told: List<RunningClaudeCode>) {
    /** Every Claude Code the hub told of that still runs. */
    val all: List<RunningClaudeCode> get() = told.filter { it.process.isAlive }

    /** The Claude Code that runs in the console of the [SessionHost] [host], or null when none does. */
    fun inTheHost(host: SessionHostId): RunningClaudeCode? = all.firstOrNull { it.sessionHost == host }

    /**
     * The Claude Code that runs as [process] or below it, or null when none does: the process of a tab without a
     * session host is its shell, and Claude Code runs below it.
     */
    fun inOrBelow(process: ProcessHandle): RunningClaudeCode? {
        val byProcess = all.associateBy { it.process.pid() }
        return (sequenceOf(process) + process.descendants().iterator().asSequence()).firstNotNullOfOrNull { byProcess[it.pid()] }
    }

    companion object {
        /** None: what is known before the hub has told of any. */
        val NONE = RunningClaudeCodes(emptyList())
    }
}
