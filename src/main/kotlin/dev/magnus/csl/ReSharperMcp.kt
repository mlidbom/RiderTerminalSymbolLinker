package dev.magnus.csl

import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.util.io.HttpRequests

/** One symbol result from the ReSharper MCP: e.g. `class DesktopGridConfigurationSpecification — C:\...\X.cs:13`. */
data class SymbolHit(val kind: String, val name: String, val file: String, val line: Int)

/**
 * Thin client for the ReSharper MCP server (joshua-light/resharper-mcp) which runs in-process in
 * Rider on 127.0.0.1:23741 and is ReSharper-backed, so it resolves and enumerates C# symbols
 * reliably — unlike the platform's GotoSymbolModel, which doesn't see them. Plain JSON-RPC over
 * HTTP; tool results are human-readable text that we parse.
 */
object ReSharperMcp {
    private const val URL = "http://127.0.0.1:23741/"
    private const val SEP = " — " // separates "<kind> <name>" from "<file>:<line>"
    private val LOG = Logger.getInstance("CSL")

    // ---- Click-time resolution -------------------------------------------------------------

    /** Exact + dot-qualified symbol matches for [query]. `null` = MCP unreachable; empty = no matches. */
    fun searchSymbol(query: String): List<SymbolHit>? {
        val text = callTool("search_symbol", "{\"query\":${jsonString(query)}}") ?: return null
        val hits = ArrayList<SymbolHit>()
        for (line in text.lineSequence()) {
            val sep = line.indexOf(SEP)
            if (sep < 0) continue
            val left = line.substring(0, sep).trim()
            val right = line.substring(sep + SEP.length).trim()
            val space = left.lastIndexOf(' ')
            val colon = right.lastIndexOf(':')
            if (space <= 0 || colon <= 0) continue
            val lineNo = right.substring(colon + 1).toIntOrNull() ?: continue
            hits.add(SymbolHit(left.substring(0, space), left.substring(space + 1), right.substring(0, colon), lineNo))
        }
        return hits
    }

    // ---- Startup enumeration ---------------------------------------------------------------

    /**
     * Every solution symbol short name (types + members), built by walking namespaces for types and
     * asking each type for its members. `null` if the MCP is unreachable at the first call.
     */
    fun enumerateSymbolNames(indicator: ProgressIndicator): Set<String>? {
        indicator.text = "Loading C# symbols…"
        val names = HashSet<String>()
        val typeNames = LinkedHashSet<String>() // de-duped, ordered
        val visitedNs = HashSet<String>()

        val rootText = browseNamespaces(null) ?: return null
        val pending = ArrayDeque<String>()
        collectBrowse(rootText, pending, visitedNs, names, typeNames)
        while (pending.isNotEmpty()) {
            indicator.checkCanceled()
            val batch = ArrayList<String>()
            while (pending.isNotEmpty() && batch.size < 50) batch.add(pending.removeFirst())
            val text = browseNamespaces(batch) ?: break
            collectBrowse(text, pending, visitedNs, names, typeNames)
        }

        val types = typeNames.toList()
        indicator.text = "Loading members of ${types.size} types…"
        var i = 0
        while (i < types.size) {
            indicator.checkCanceled()
            indicator.fraction = i.toDouble() / types.size.coerceAtLeast(1)
            val end = minOf(i + 40, types.size)
            getMembers(types.subList(i, end))?.let { parseMembers(it, names) }
            i = end
        }
        LOG.info("CSL symbol index: ${names.size} names (${types.size} types)")
        return names
    }

    private fun collectBrowse(
        text: String,
        pending: ArrayDeque<String>,
        visitedNs: MutableSet<String>,
        names: MutableSet<String>,
        typeNames: MutableSet<String>,
    ) {
        var mode = 0 // 1 = child namespaces, 2 = types
        for (raw in text.lineSequence()) {
            val t = raw.trim()
            when {
                t == "child namespaces:" -> mode = 1
                t == "types:" -> mode = 2
                t.isEmpty() || t.startsWith("===") || t.startsWith("namespace:") -> mode = 0
                mode == 1 -> {
                    val ns = t.substringBefore(" (").trim()
                    if (ns.isNotEmpty() && visitedNs.add(ns)) pending.add(ns)
                }
                mode == 2 -> {
                    val name = t.substringBefore(SEP).substringAfterLast(' ').trim()
                    if (name.isNotEmpty()) { names.add(name); typeNames.add(name) }
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

    private fun browseNamespaces(namespaceNames: List<String>?): String? =
        if (namespaceNames.isNullOrEmpty()) {
            callTool("browse_namespace", "{}")
        } else {
            callTool("browse_namespace", "{\"namespaceNames\":[${namespaceNames.joinToString(",") { jsonString(it) }}]}")
        }

    private fun getMembers(typeNames: List<String>): String? {
        val symbols = typeNames.joinToString(",") { "{\"symbolName\":${jsonString(it)}}" }
        return callTool("get_symbol_info", "{\"includeMembers\":true,\"symbols\":[$symbols]}")
    }

    // ---- transport -------------------------------------------------------------------------

    private fun callTool(tool: String, argsJson: String): String? {
        val body =
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\"," +
                "\"params\":{\"name\":\"$tool\",\"arguments\":$argsJson}}"
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
}
