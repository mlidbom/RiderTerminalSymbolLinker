package dev.magnus.csl

import com.google.gson.Gson
import com.google.gson.JsonArray
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.util.io.HttpRequests

/**
 * A declaration a link can open: [qualifiedName] as C# writes it, in [file] at [line] and [column], both 1-based.
 * [kind] is the C# keyword for what it declares, such as `class` or `method`.
 */
data class SymbolHit(val kind: String, val qualifiedName: String, val file: String, val line: Int, val column: Int)

/**
 * One loaded solution from `list_solutions`. The value to pass back as `solutionName` is [routingKey], not
 * [name]: the server keys `solutionName` on a match that includes the bare name, so when several loaded
 * solutions share a [name] it resolves to whichever same-named solution it picks first. Two git worktrees of
 * the same repo are exactly this case — both name their solution e.g. "Vantage" — and only the server's
 * `uniquePathSegment` ("Vantage-wt-1" vs "Vantage") routes to the intended one. [uniquePathSegment] is `null`
 * when the server leaves it out, and [routingKey] then falls back to the [name].
 */
data class SolutionInfo(val name: String, val path: String, val uniquePathSegment: String? = null) {
    /** The `solutionName` value that routes to exactly this solution, even when [name] collides. */
    val routingKey: String get() = uniquePathSegment ?: name
}

/**
 * Client for the .NET Analysis&Refactoring MCP, the plugin this one depends on, whose server runs inside Rider's
 * ReSharper backend on 127.0.0.1, port 23751 by default. It resolves and enumerates C# symbols through ReSharper,
 * unlike the platform's GotoSymbolModel, which doesn't see them. Plain JSON-RPC over HTTP.
 *
 * It reads the tools' answers in their JSON form alone, which the argument `"format": "json"` asks for: the types in
 * McpJsonContract.kt. An MCP too old to have that form ignores the argument and answers text, which this reads as
 * no answer, and logs.
 */
object ReSharperMcp {
    private const val DEFAULT_PORT = 23751

    // The server binds the port this variable names, when set. Rider's ReSharper backend is started by this
    // frontend process and inherits its environment, so reading it here finds the port the server bound.
    private val URL = "http://127.0.0.1:${System.getenv("DOTNET_ANALYSIS_AND_REFACTORING_MCP_PORT")?.toIntOrNull() ?: DEFAULT_PORT}/"
    private val LOG = Logger.getInstance("CSL")
    private val gson = Gson()

    /** How many namespaces one call lists while the namespaces are found, before their types are read. */
    private const val NAMESPACES_PER_CALL = 50

    /** The global namespace, as `browse_namespace` names it. */
    private const val GLOBAL_NAMESPACE = ""

    // ---- Solution discovery ----------------------------------------------------------------

    /**
     * Every solution currently loaded in the MCP. Used to map a Rider project to the `solutionName`
     * the other tools need when more than one solution is open. `null` if the MCP is unreachable.
     */
    fun listSolutions(): List<SolutionInfo>? = callTool("list_solutions", JsonObject(), null)?.let(::solutionsIn)

    internal fun solutionsIn(answer: JsonObject): List<SolutionInfo> = gson.fromJson(answer, SolutionsAnswer::class.java).solutions

    // ---- Click-time resolution (exact by name) ---------------------------------------------

    /**
     * The declarations of the symbol named [name], a bare name or the end of a qualified one, such as `Circle.Area`.
     * One opens at once, several show a picker. Compiled symbols are left out: they have no source to open.
     * `null` = the MCP is unreachable or couldn't answer; empty = no such symbol.
     */
    fun goToDefinition(name: String, solutionName: String?): List<SymbolHit>? =
        callTool("go_to_definition", inJsonForm().apply { addProperty("symbolName", name) }, solutionName)?.let(::declarationsIn)

    internal fun declarationsIn(answer: JsonObject): List<SymbolHit> =
        gson.fromJson(answer, DeclarationsAnswer::class.java).declarations.mapNotNull { declaration ->
            declaration.file?.let { SymbolHit(declaration.kind, declaration.qualifiedName, it, declaration.line!!, declaration.column!!) }
        }

    // ---- Startup enumeration ---------------------------------------------------------------

    /**
     * The names of every type in the solution and of their members, nested types included, as [SymbolNamesGatherer]
     * gathers them. First the namespaces are found, a batch of them per call; then each namespace's types are listed
     * with their members, one namespace per call, which the progress bar counts.
     *
     * Sequential by design: the ReSharper backend serializes analysis, so fanning calls out across threads measured
     * ~1.2x at best — not worth the nondeterminism — and one namespace per call keeps each call short, so that other
     * clients' calls don't wait long behind ours. Its duration is hidden from the user by the disk cache
     * (see [SymbolIndexCache]), which serves the index while this runs in the background.
     *
     * `null` means "no usable index right now": the MCP was unreachable, or answered an error, as it does while the
     * solution is still loading. Callers must not cache or display it (see [SymbolIndexLoader]'s retry).
     */
    fun enumerateSymbolNames(indicator: ProgressIndicator, solutionName: String): SymbolNames? {
        val started = System.nanoTime()
        indicator.isIndeterminate = true // How many namespaces there are is known only once they're all found.
        indicator.text = "Terminal Symbol Linker:Finding namespaces…"
        val namespaces = allNamespaces(indicator, solutionName) ?: return null

        indicator.text = "Terminal Symbol Linker:Loading types and members…"
        indicator.isIndeterminate = false
        indicator.fraction = 0.0
        val gatherer = SymbolNamesGatherer()
        for ((index, namespace) in namespaces.withIndex()) {
            indicator.checkCanceled()
            val listing = browse(listOf(namespace), includeMembers = true, solutionName)?.single() ?: return null
            listing.error?.let { LOG.info("CSL: namespace '$namespace' not listed: $it") }
            listing.types.forEach(gatherer::add)
            indicator.fraction = (index + 1).toDouble() / namespaces.size
        }
        val symbols = gatherer.gathered()
        LOG.info("CSL symbol index: ${symbols.short.size} names, ${symbols.combined.size} combined, from ${namespaces.size} namespaces in ${(System.nanoTime() - started) / 1_000_000} ms")
        return symbols
    }

    /** The qualified names of every namespace in the solution, the global one first. */
    private fun allNamespaces(indicator: ProgressIndicator, solutionName: String): List<String>? {
        val found = ArrayList<String>()
        val pending = ArrayDeque(listOf(GLOBAL_NAMESPACE))
        while (pending.isNotEmpty()) {
            indicator.checkCanceled()
            val batch = List(minOf(NAMESPACES_PER_CALL, pending.size)) { pending.removeFirst() }
            found += batch
            val listings = browse(batch, includeMembers = false, solutionName) ?: return null
            listings.forEach { pending += it.childNamespaces }
        }
        return found
    }

    private fun browse(namespaces: List<String>, includeMembers: Boolean, solutionName: String): List<NamespaceListing>? {
        val arguments = inJsonForm().apply {
            add("namespaceNames", JsonArray().apply { namespaces.forEach(::add) })
            addProperty("includeMembers", includeMembers)
        }
        return callTool("browse_namespace", arguments, solutionName)?.let { gson.fromJson(it, NamespacesAnswer::class.java).namespaces }
    }

    // ---- transport -------------------------------------------------------------------------

    /** Arguments that ask a tool for its JSON form. */
    private fun inJsonForm(): JsonObject = JsonObject().apply { addProperty("format", "json") }

    /**
     * Calls [tool] with [arguments], and [solutionName] when set, so the MCP targets the right open solution. Returns
     * the JSON object the tool answers, or `null` when the MCP is unreachable, answers an error, or answers text.
     */
    private fun callTool(tool: String, arguments: JsonObject, solutionName: String?): JsonObject? {
        solutionName?.let { arguments.addProperty("solutionName", it) }
        val request = JsonObject().apply {
            addProperty("jsonrpc", "2.0")
            addProperty("id", 1)
            addProperty("method", "tools/call")
            add("params", JsonObject().apply {
                addProperty("name", tool)
                add("arguments", arguments)
            })
        }
        val response = try {
            HttpRequests.post(URL, "application/json")
                .accept("application/json")
                .connect { it.write(request.toString()); it.readString() }
        } catch (e: Throwable) {
            LOG.warn(".NET Analysis&Refactoring MCP unreachable at $URL ($tool): ${e.message}")
            return null
        }
        return toolAnswerIn(tool, response)
    }

    /** The JSON object in a JSON-RPC [response]'s `result.content[0].text`: the tool's answer. */
    private fun toolAnswerIn(tool: String, response: String): JsonObject? {
        val envelope = JsonParser.parseString(response).asJsonObject
        envelope.getAsJsonObject("error")?.let {
            LOG.warn(".NET Analysis&Refactoring MCP refused $tool: $it")
            return null
        }
        val text = envelope.getAsJsonObject("result").getAsJsonArray("content")[0].asJsonObject["text"].asString
        if (!text.trimStart().startsWith("{")) {
            LOG.warn(".NET Analysis&Refactoring MCP answered $tool with text, not JSON: it's older than the version this plugin requires.")
            return null
        }
        val answer = JsonParser.parseString(text).asJsonObject
        answer["error"]?.let {
            LOG.info(".NET Analysis&Refactoring MCP couldn't answer $tool: ${it.asString}")
            return null
        }
        return answer
    }
}
