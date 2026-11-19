package dev.magnus.claudesessions.hub

import com.google.gson.JsonObject
import com.google.gson.JsonParser
import dev.magnus.claudesessions.sessionhosts.SessionHost
import dev.magnus.claudesessions.sessionhosts.SessionHostId
import dev.magnus.claudesessions.sessionhosts.SessionHostLabel
import java.nio.file.Path
import java.time.Duration

/**
 * The [SessionHost]s of the hub's Claude Code folder, as the hub starts, lists and ends them: `POST /session-hosts`,
 * `GET /session-hosts` and `POST /session-hosts/{id}/end` of its local API, whose contract is
 * `~/.claude/dev_docs/the-hubs-local-api.md`, "Session hosts". Each request goes to whichever hub answers it, as
 * [RequestsToTheHub] says: a host outlives the hub that started it, and the next hub finds it.
 */
class SessionHosts internal constructor(private val requests: RequestsToTheHub) {
    /**
     * Starts a session host whose console runs [shell], the shell's command line, in [directory], with [environment] on
     * top of the one Windows gives a process started afresh, labelled [label], and returns it once it listens. Its console
     * starts at the hub's size, 120 by 30, until its first attach client gives it the tab's.
     */
    suspend fun start(shell: String, directory: Path, environment: Map<String, String>, label: SessionHostLabel): SessionHostsAnswer<SessionHost> {
        val body = JsonObject().apply {
            addProperty("shell", shell)
            addProperty("directory", directory.toAbsolutePath().toString())
            add("environment", JsonObject().apply { environment.forEach(::addProperty) })
            addProperty("label", label.text)
        }
        return when (val answer = requests.post("/session-hosts", body, ANSWERING_MAY_TAKE)) {
            is HubAnswer.NoHubAnswered -> SessionHostsAnswer.NotDone(answer.why)
            is HubAnswer.Answered -> when (answer.status) {
                201 -> SessionHostsAnswer.Done(hostIn(JsonParser.parseString(answer.body).asJsonObject.getAsJsonObject("sessionHost")))
                400 -> SessionHostsAnswer.NotDone(errorIn(answer))
                else -> notDoneOrABug(answer, "start a session host")
            }
        }
    }

    /** The session hosts that run, in the order they started. */
    suspend fun running(): SessionHostsAnswer<List<SessionHost>> =
        when (val answer = requests.get("/session-hosts", ANSWERING_MAY_TAKE)) {
            is HubAnswer.NoHubAnswered -> SessionHostsAnswer.NotDone(answer.why)
            is HubAnswer.Answered -> when (answer.status) {
                200 -> SessionHostsAnswer.Done(hostsIn(answer.body))
                else -> notDoneOrABug(answer, "list the session hosts")
            }
        }

    /**
     * Ends [host]'s session, as closing its terminal does: the shell and Claude Code get the console's close event.
     * Answered once the host has ended, or has been asked to for [ENDING_MAY_TAKE]. A host that no longer runs, as one
     * whose shell has exited, has ended already.
     */
    suspend fun end(host: SessionHostId): SessionHostsAnswer<Unit> =
        when (val answer = requests.post("/session-hosts/${host.value}/end", body = null, ENDING_MAY_TAKE)) {
            is HubAnswer.NoHubAnswered -> SessionHostsAnswer.NotDone(answer.why)
            is HubAnswer.Answered -> when {
                answer.status == 204 -> SessionHostsAnswer.Done(Unit)
                answer.status == 404 && answer.body.isNotBlank() -> SessionHostsAnswer.Done(Unit)
                answer.status == 504 -> SessionHostsAnswer.NotDone(errorIn(answer))
                else -> notDoneOrABug(answer, "end a session host")
            }
        }

    /**
     * Not done, for a hub older than its session hosts, whose API answers 404 with no body to a path it doesn't serve;
     * for any other answer the contract doesn't have, a bug of the plugin's.
     */
    private fun notDoneOrABug(answer: HubAnswer.Answered, asked: String): SessionHostsAnswer<Nothing> {
        check(answer.status == 404 && answer.body.isBlank()) { "The hub of build ${answer.build} answered a request to $asked with ${answer.status}: ${answer.body}" }
        return SessionHostsAnswer.NotDone("The Claude tooling's hub of build ${answer.build} has no session hosts: it's older than them.")
    }

    private fun errorIn(answer: HubAnswer.Answered): String = JsonParser.parseString(answer.body).asJsonObject.get("error").asString

    internal companion object {
        /** Starting a host, listing and ending them are quick: a hub that takes longer is stuck. */
        private val ANSWERING_MAY_TAKE: Duration = Duration.ofSeconds(30)

        /** The hub answers once the host has ended, or after 15 seconds. */
        private val ENDING_MAY_TAKE: Duration = Duration.ofSeconds(30)

        /** The session hosts the hub's answer to `GET /session-hosts`, [json], tells of. */
        internal fun hostsIn(json: String): List<SessionHost> = JsonParser.parseString(json).asJsonObject.getAsJsonArray("sessionHosts").map { hostIn(it.asJsonObject) }

        /** The session host [host] tells of, as `GET /session-hosts` lists it. */
        internal fun hostIn(host: JsonObject): SessionHost =
            SessionHost(
                id = SessionHostId(host.get("id").asString),
                shell = ProcessHandle.of(host.get("shellProcessId").asLong).orElse(null),
                label = host.get("label").takeUnless { it.isJsonNull }?.asString,
                attached = host.get("attached").asBoolean,
                attachCommand = host.getAsJsonArray("attachCommand").map { it.asString },
            )
    }
}
