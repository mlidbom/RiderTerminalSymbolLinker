package dev.magnus.csl

/**
 * Finds the [SymbolReference]s in a line of terminal output: the names of the solution's types and members, each alone
 * or after the names of the types and namespaces it is in, and of the namespaces they are in. The [DeclaredNames]
 * decide, not a word's shape: `_field`, `ALL_CAPS` and `camelCase` link alike.
 *
 * Words joined by dots link as one symbol as far as they are the end of its qualified name, looking from the left and
 * taking the most words that are: `Geometry.Shapes.Circle.Area`, `Shapes.Circle` and `Circle.Area` each link as one, and
 * so does a namespace's name, or its end, from two words on, as `Geometry.Shapes`. Words that are no part of the name,
 * as `Foo.Bar` in `Foo.Bar.Circle.Area`, are passed over, and link each on its own if they name a symbol. A word links
 * on its own from [SHORTEST_WORD_THAT_LINKS_ON_ITS_OWN] letters: a name such as `Id` links, as a false link costs Magnus
 * nothing and a missing one does (2026-11-26); a single letter is a loop's counter more often than a name.
 *
 * Names inside a file path or URL ([PathSpans]) are skipped: Rider already links those, and a symbol link would
 * overwrite a working file link.
 */
object SymbolReferences {

    /** Identifiers joined by dots, as C# writes them in ASCII: `Circle.Area`, or a bare `SymbolIndex`. */
    private val wordsJoinedByDots = Regex("""[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*""")

    private const val SHORTEST_WORD_THAT_LINKS_ON_ITS_OWN = 2

    fun find(line: String, declared: DeclaredNames): List<SymbolReference> {
        val pathsAndUrls = PathSpans.find(line)
        return wordsJoinedByDots.findAll(line)
            .flatMap { WordsJoinedByDots(it, declared).references() }
            .filterNot { reference -> pathsAndUrls.any { reference.range overlaps it } }
            .toList()
    }

    /** Whether the two inclusive ranges share an offset. */
    private infix fun IntRange.overlaps(other: IntRange): Boolean = first <= other.last && other.first <= last

    /** One run of words joined by dots in a line, such as `Foo.Bar.Circle.Area`, and the references it holds. */
    private class WordsJoinedByDots(run: MatchResult, private val declared: DeclaredNames) {
        private val words: List<Word> = buildList {
            var start = run.range.first
            for (text in run.value.split('.')) {
                add(Word(start..<start + text.length, text))
                start += text.length + 1 // past the dot
            }
        }

        /**
         * The types, members and namespaces its words name: from each word, the most words that name one, a type or
         * member before a namespace of as many words. A namespace links from two words: one alone, such as `Chat`, is a
         * word of prose more often than a namespace's name (Magnus, 2026-11-26).
         */
        fun references(): Sequence<SymbolReference> = sequence {
            var next = 0
            while (next < words.size) {
                val reference = theMostWordsThatNameOneFrom(next)
                if (reference == null) {
                    next++
                } else {
                    yield(reference)
                    next += reference.name.count { it == '.' } + 1
                }
            }
        }

        private fun theMostWordsThatNameOneFrom(first: Int): SymbolReference? {
            for (last in words.indices.last downTo first) {
                val range = words[first].range.first..words[last].range.last
                val name = nameOf(first, last)
                val isOneWord = last == first
                if ((!isOneWord || name.length >= SHORTEST_WORD_THAT_LINKS_ON_ITS_OWN) && declared.namesATypeOrMember(name)) {
                    return SymbolReference.ToATypeOrMember(range, name)
                }
                if (!isOneWord && declared.namesANamespace(name)) return SymbolReference.ToANamespace(range, name)
            }
            return null
        }

        private fun nameOf(first: Int, last: Int): String = words.subList(first, last + 1).joinToString(".") { it.text }
    }

    /** An identifier in a line, and where it is, inclusive. */
    private class Word(val range: IntRange, val text: String)
}
