package dev.magnus.csl

import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.util.io.HttpRequests
import java.util.Collections
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicInteger

/** ReSharper reports compiler-synthesized members (e.g. positional-record properties) with this file. */
private const val NO_SOURCE = "[no source]"

/** One symbol location from the ReSharper MCP. [name] is a short or qualified name for display. */
data class SymbolHit(val kind: String, val name: String, val file: String, val line: Int) {
    /** Whether this hit points at a navigable source line. Synthesized members report [NO_SOURCE]:0. */
    val hasSource: Boolean get() = file != NO_SOURCE && line >= 1
}

/** One loaded solution from `list_solutions`. [name] is the value to pass as `solutionName`. */
data class SolutionInfo(val name: String, val path: String)

/**
 * Thin client for the ReSharper MCP server (joshua-light/resharper-mcp) which runs in-process in
 * Rider on 127.0.0.1:23741 and is ReSharper-backed, so it resolves and enumerates C# symbols
 * reliably — unlike the platform's GotoSymbolModel, which doesn't see them. Plain JSON-RPC over
 * HTTP; tool results are human-readable text (or a small JSON blob for ambiguity) that we parse.
 */
object ReSharperMcp {
    private const val URL = "http://127.0.0.1:23741/"
    private const val SEP = " — " // separates "<kind> <name> : <type>" from "<file>:<line>:<col>"
    private const val MEMBER_FETCH_THREADS = 6
    private val LOG = Logger.getInstance("CSL")

    // "<file>:<line>:<col>" (drive colon stays inside the greedy file group)
    private val LOCATION = Regex("""^(.*):(\d+):(\d+)$""")

    // one entry of go_to_definition's ambiguity payload: {"qualifiedName":..,"kind":..,"file":..,"line":N}
    private val CANDIDATE = Regex(
        """"qualifiedName"\s*:\s*"([^"]*)"\s*,\s*"kind"\s*:\s*"([^"]*)"\s*,\s*"file"\s*:\s*"([^"]*)"\s*,\s*"line"\s*:\s*(\d+)""",
    )

    // one entry of list_solutions: {"name":"Vantage","path":"C:\\…\\Vantage.slnx", …}
    private val SOLUTION = Regex(""""name"\s*:\s*"([^"]*)"\s*,\s*"path"\s*:\s*"([^"]*)"""")

    // ---- Solution discovery ----------------------------------------------------------------

    /**
     * Every solution currently loaded in the MCP. Used to map a Rider project to the `solutionName`
     * the other tools need when more than one solution is open. `null` if the MCP is unreachable.
     */
    fun listSolutions(): List<SolutionInfo>? {
        val text = callTool("list_solutions", "{}", null) ?: return null
        return SOLUTION.findAll(text)
            .map { SolutionInfo(it.groupValues[1], it.groupValues[2].replace("\\\\", "\\")) }
            .toList()
    }

    // ---- Click-time resolution (exact by name) ---------------------------------------------

    /**
     * Declarations of the symbol exactly named [name]. One hit -> jump; several -> picker.
     * A compiler-synthesized member (e.g. a positional-record property) has no source of its own, so
     * we navigate to its declaring type instead — that source line is where the member is written
     * (the record's primary-constructor parameter). `null` = MCP unreachable; empty = no such symbol.
     */
    fun goToDefinition(name: String, solutionName: String?): List<SymbolHit>? {
        val hits = resolveByName(name, solutionName) ?: return null
        return hits.mapNotNull { if (it.hasSource) it else resolveViaContainingType(it, solutionName) }
    }

    private fun resolveByName(name: String, solutionName: String?): List<SymbolHit>? {
        val text = callTool("go_to_definition", "{\"symbolName\":${jsonString(name)}}", solutionName) ?: return null
        if (text.contains("\"candidates\"")) {
            return CANDIDATE.findAll(text).map { m ->
                SymbolHit(m.groupValues[2], m.groupValues[1], m.groupValues[3].replace("\\\\", "\\"), m.groupValues[4].toInt())
            }.toList()
        }
        val hits = ArrayList<SymbolHit>()
        for (line in text.lineSequence()) {
            val sep = line.indexOf(SEP)
            if (sep < 0) continue
            val left = line.substring(0, sep).trim()
            val loc = LOCATION.find(line.substring(sep + SEP.length).trim()) ?: continue
            val head = left.substringBefore(" : ")
            hits.add(
                SymbolHit(
                    head.substringBeforeLast(' ', ""),
                    head.substringAfterLast(' '),
                    loc.groupValues[1],
                    loc.groupValues[2].toInt(),
                ),
            )
        }
        return hits
    }

    /**
     * A synthesized [member] (no source of its own) resolves to its declaring type's source line.
     * The containing type comes from the member's qualified name (`Ns.Type.Member` -> `Ns.Type`);
     * `null` if we only have a short name or the type itself has no navigable source.
     */
    private fun resolveViaContainingType(member: SymbolHit, solutionName: String?): SymbolHit? {
        val containingType = member.name.substringBeforeLast('.', "")
        if (containingType.isEmpty()) return null
        val typeHit = resolveByName(containingType, solutionName)?.firstOrNull { it.hasSource } ?: return null
        return member.copy(file = typeHit.file, line = typeHit.line)
    }

    // ---- Startup enumeration ---------------------------------------------------------------

    /**
     * Every solution symbol short name (types + members). Types come from walking namespaces;
     * members from one `get_symbol_info` per type, keyed by the namespace-qualified name so the
     * result is unambiguous. (The batch form of `get_symbol_info` errors with "Universal", and
     * `list_symbols_in_file` returns nothing — single qualified calls are the reliable path.)
     * `null` if the MCP is unreachable at the first call.
     */
    fun enumerateSymbolNames(indicator: ProgressIndicator, solutionName: String?): Set<String>? {
        indicator.text = "Loading C# symbols…"
        val names = Collections.synchronizedSet(HashSet<String>())
        val qualifiedTypes = LinkedHashSet<String>()
        val visitedNs = HashSet<String>()

        val rootText = browseNamespaces(null, solutionName) ?: return null
        val pending = ArrayDeque<String>()
        collectBrowse(rootText, pending, visitedNs, names, qualifiedTypes)
        while (pending.isNotEmpty()) {
            indicator.checkCanceled()
            val batch = ArrayList<String>()
            while (pending.isNotEmpty() && batch.size < 50) batch.add(pending.removeFirst())
            collectBrowse(browseNamespaces(batch, solutionName) ?: break, pending, visitedNs, names, qualifiedTypes)
        }

        val types = qualifiedTypes.toList()
        indicator.text = "Loading members of ${types.size} types…"
        val done = AtomicInteger(0)
        val pool = Executors.newFixedThreadPool(MEMBER_FETCH_THREADS)
        try {
            val futures = types.map { qualifiedName ->
                pool.submit {
                    if (!indicator.isCanceled) {
                        getMembersOf(qualifiedName, solutionName)?.let { parseMembers(it, names) }
                        indicator.fraction = done.incrementAndGet().toDouble() / types.size
                    }
                }
            }
            for (future in futures) {
                indicator.checkCanceled()
                try {
                    future.get()
                } catch (e: Exception) {
                    // tolerate a single type's failure; keep the rest
                }
            }
        } finally {
            pool.shutdownNow()
        }
        LOG.info("CSL symbol index: ${names.size} names (${types.size} types)")
        return HashSet(names)
    }

    private fun collectBrowse(
        text: String,
        pending: ArrayDeque<String>,
        visitedNs: MutableSet<String>,
        names: MutableSet<String>,
        qualifiedTypes: MutableSet<String>,
    ) {
        var mode = 0 // 1 = child namespaces, 2 = types
        var namespace = ""
        for (raw in text.lineSequence()) {
            val t = raw.trim()
            when {
                t.startsWith("namespace:") -> {
                    namespace = t.substringAfter("namespace:").trim().let { if (it == "(root)") "" else it }
                    mode = 0
                }
                t == "child namespaces:" -> mode = 1
                t == "types:" -> mode = 2
                t.isEmpty() || t.startsWith("===") -> mode = 0
                mode == 1 -> {
                    val ns = t.substringBefore(" (").trim()
                    if (ns.isNotEmpty() && visitedNs.add(ns)) pending.add(ns)
                }
                mode == 2 -> {
                    val short = t.substringBefore(SEP).substringAfterLast(' ').trim()
                    if (short.isNotEmpty()) {
                        names.add(short)
                        qualifiedTypes.add(if (namespace.isEmpty()) short else "$namespace.$short")
                    }
                }
            }
        }
    }

    private fun parseMembers(text: String, out: MutableSet<String>) {
        var inMembers = false
        for (raw in text.lineSequence()) {
            val t = raw.trim()
            when {
                t == "members:" -> inMembers = true
                t.isEmpty() || t.startsWith("===") -> inMembers = false
                inMembers -> {
                    val beforeParen = t.substringBefore("(")
                    val beforeColon = if (beforeParen.contains(" : ")) beforeParen.substringBefore(" : ") else beforeParen
                    val name = beforeColon.trim().substringAfterLast(' ')
                    if (name.isNotEmpty()) out.add(name)
                }
            }
        }
    }

    private fun browseNamespaces(namespaceNames: List<String>?, solutionName: String?): String? =
        if (namespaceNames.isNullOrEmpty()) {
            callTool("browse_namespace", "{}", solutionName)
        } else {
            callTool("browse_namespace", "{\"namespaceNames\":[${namespaceNames.joinToString(",") { jsonString(it) }}]}", solutionName)
        }

    private fun getMembersOf(qualifiedTypeName: String, solutionName: String?): String? =
        callTool("get_symbol_info", "{\"symbolName\":${jsonString(qualifiedTypeName)},\"includeMembers\":true}", solutionName)

    // ---- transport -------------------------------------------------------------------------

    /** [solutionName], when set, is merged into [argsJson] so the MCP targets the right open solution. */
    private fun callTool(tool: String, argsJson: String, solutionName: String?): String? {
        val args = if (solutionName == null) argsJson else withSolution(argsJson, solutionName)
        val body =
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\"," +
                "\"params\":{\"name\":\"$tool\",\"arguments\":$args}}"
        val response = try {
            HttpRequests.post(URL, "application/json")
                .accept("application/json")
                .connect { request ->
                    request.write(body)
                    request.readString()
                }
        } catch (e: Throwable) {
            LOG.warn("ReSharper MCP unreachable ($tool): ${e.message}")
            return null
        }
        return extractText(response)
    }

    /** Pull the concatenated result.content[].text out of the JSON-RPC envelope without a JSON lib. */
    private fun extractText(response: String): String {
        val out = StringBuilder()
        val key = "\"text\":\""
        var idx = response.indexOf(key)
        while (idx >= 0) {
            var i = idx + key.length
            while (i < response.length) {
                val c = response[i]
                if (c == '\\' && i + 1 < response.length) {
                    when (response[i + 1]) {
                        'n' -> out.append('\n')
                        'r' -> out.append('\r')
                        't' -> out.append('\t')
                        '"' -> out.append('"')
                        '\\' -> out.append('\\')
                        '/' -> out.append('/')
                        'u' -> if (i + 5 < response.length) {
                            out.append(response.substring(i + 2, i + 6).toInt(16).toChar()); i += 4
                        }
                        else -> out.append(response[i + 1])
                    }
                    i += 2
                } else if (c == '"') {
                    break
                } else {
                    out.append(c); i++
                }
            }
            out.append('\n')
            idx = response.indexOf(key, i)
        }
        return out.toString()
    }

    private fun jsonString(s: String): String =
        "\"" + s.replace("\\", "\\\\").replace("\"", "\\\"") + "\""

    /** Add `"solutionName":…` to an arguments object. [argsJson] is always our own `{…}` literal. */
    private fun withSolution(argsJson: String, solutionName: String): String {
        val field = "\"solutionName\":${jsonString(solutionName)}"
        val trimmed = argsJson.trim()
        return if (trimmed == "{}") "{$field}" else trimmed.dropLast(1) + ",$field}"
    }
}
