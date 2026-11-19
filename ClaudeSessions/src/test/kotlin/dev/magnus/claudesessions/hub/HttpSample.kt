package dev.magnus.claudesessions.hub

/**
 * One of the hub's samples of its answers, as the hub's specifications keep them and the plugin's contract test copies
 * them: the HTTP the answer goes out as, its status line, the headers that are part of the contract, a blank line, and
 * the body.
 */
class HttpSample(text: String) {
    private val lines = text.replace("\r\n", "\n").lines()
    private val blankLine = lines.indexOf("")

    /** The answer's status, such as 200. */
    val status: Int = lines.first().split(' ')[1].toInt()

    /** The value of the answer's header [name], or null when the sample has none. */
    fun header(name: String): String? =
        lines.subList(1, blankLine).map { it.split(':', limit = 2) }.firstOrNull { it[0].equals(name, ignoreCase = true) }?.get(1)?.trim()

    /** The body's lines, as a reader of the answer reads them. */
    val bodyLines: List<String> = lines.drop(blankLine + 1)

    val body: String get() = bodyLines.joinToString("\n")

    companion object {
        /** The sample [file], from the copies beside the contract test. */
        fun named(file: String): String =
            HttpSample::class.java.getResource("samples/$file")?.readText() ?: error("No sample named $file beside the contract test.")
    }
}
