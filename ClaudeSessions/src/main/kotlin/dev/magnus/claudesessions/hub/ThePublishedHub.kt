package dev.magnus.claudesessions.hub

import com.intellij.openapi.diagnostic.logger
import java.nio.file.Files
import java.nio.file.Path
import java.util.concurrent.TimeUnit

/**
 * The Claude tooling's hub as it's published, to `~/.claude/bin`, which Magnus's IDEs follow: started by every Claude
 * Code session's SessionStart hook, and by the plugin when it must type into a shell before any Claude Code runs, as
 * after a reboot, when the IDE resumes its sessions.
 */
internal object ThePublishedHub {
    private val LOG = logger<ThePublishedHub>()

    private val client: Path = Path.of(System.getProperty("user.home"), ".claude", "bin", "ClaudeTooling.exe")

    /**
     * Whether the IDE follows the published hub, and not one tried out apart from it, as the sandbox IDE does: whoever
     * tries a hub starts it, with the environment it needs.
     */
    val isTheOneThisIdeFollows: Boolean get() = !HubAnnouncement.isOfATrialHub

    /**
     * Starts the hub, as `ClaudeTooling.exe hub start` does, which returns once it has started the hub: detached, and
     * out of the IDE's job object, so that the hub outlives the IDE. The hub then announces itself, which
     * [HubAnnouncement.read] reads.
     */
    fun start() {
        if (!Files.exists(client)) return LOG.warn("No Claude tooling hub runs, and none can be started: $client isn't there.")
        LOG.info("No Claude tooling hub runs: starting one, with $client hub start.")
        val starting = ProcessBuilder(client.toString(), "hub", "start")
            .redirectOutput(ProcessBuilder.Redirect.DISCARD).redirectError(ProcessBuilder.Redirect.DISCARD).start()
        if (!starting.waitFor(STARTING_MAY_TAKE_SECONDS, TimeUnit.SECONDS)) LOG.warn("$client hub start didn't exit within $STARTING_MAY_TAKE_SECONDS seconds.")
    }

    private const val STARTING_MAY_TAKE_SECONDS = 30L
}
