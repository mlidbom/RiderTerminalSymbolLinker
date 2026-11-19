package dev.magnus.claudesessions

/**
 * A kind of shell that a [ClaudeSessionTab]'s terminal runs, by how it reads a command typed into it: so that the tab
 * types a [ClaudeCodeCommand] such that each of its words reaches Claude Code as it is, whatever characters it has, as
 * a session's name may have any.
 *
 * Each word that no shell treats specially, such as `claude`, `--resume` and a session's id, is typed as it is, so the
 * line reads as a user would type it. Any other is quoted, as the shell reads quotes.
 */
enum class Shell {
    /**
     * PowerShell 7, `pwsh`: a word in single quotes is taken as it is, each single quote in it doubled. From 7.3 on, it
     * hands each word on to the program as [WindowsProgramWords] reads it.
     */
    POWERSHELL {
        override fun quoted(word: String): String = inPowerShellsSingleQuotes(word)
    },

    /**
     * Windows PowerShell, `powershell`: single quotes, as [POWERSHELL]. But it hands a word on to the program as it is,
     * in double quotes when it has whitespace, so the program would misread a `"` in it, or a backslash before the
     * closing `"`: they're escaped as [WindowsProgramWords] reads them first.
     */
    WINDOWS_POWERSHELL {
        override fun quoted(word: String): String {
            val quotesEscaped = WindowsProgramWords.quotesEscaped(word)
            val inDoubleQuotes = word.any(Char::isWhitespace)
            return inPowerShellsSingleQuotes(if (inDoubleQuotes) WindowsProgramWords.trailingBackslashesDoubled(quotesEscaped) else quotesEscaped)
        }
    },

    /**
     * Windows' `cmd`, which hands the line to the program as it's typed: a word goes in double quotes as
     * [WindowsProgramWords] reads them. Then each character cmd itself would act on, such as `&`, gets a `^` before it,
     * where cmd doesn't take it as quoted: each `"` begins or ends what cmd takes as quoted, whether a backslash is before
     * it or not.
     *
     * cmd puts the value of an environment variable in place of its name between two `%`, as `%PATH%`, quoted or not,
     * and has no escape for that on its command line: a name with one reaches Claude Code changed.
     */
    CMD {
        override fun quoted(word: String): String = caretBeforeWhatCmdWouldActOn(WindowsProgramWords.quoted(word))

        /** [line] with a `^` before each character cmd would act on, outside what it takes as quoted. */
        private fun caretBeforeWhatCmdWouldActOn(line: String): String = buildString {
            var quoted = false
            for (character in line) {
                if (character == '"') quoted = !quoted
                else if (!quoted && character in "^&|<>()") append('^')
                append(character)
            }
        }
    },

    /** A POSIX shell, such as bash, zsh or sh: a word in single quotes, each single quote in it ended, escaped, and begun again. */
    POSIX {
        override fun quoted(word: String): String = "'" + word.replace("'", """'\''""") + "'"
    };

    /** The line to type into the shell to run [command]. */
    fun lineThatRuns(command: ClaudeCodeCommand): String =
        command.words.joinToString(" ") { if (PLAIN_WORD.matches(it)) it else quoted(it) }

    /** [word], quoted so that the program the shell runs gets it as it is. */
    protected abstract fun quoted(word: String): String

    /**
     * [text] in single quotes, as PowerShell takes it as it is: each single quote in it doubled, as are the typographic
     * ones, which PowerShell takes for single quotes too.
     */
    protected fun inPowerShellsSingleQuotes(text: String): String =
        "'" + text.map { if (it in POWERSHELLS_SINGLE_QUOTES) "$it$it" else "$it" }.joinToString("") + "'"

    companion object {
        /** A word that no shell treats specially, typed as it is. */
        private val PLAIN_WORD = Regex("[A-Za-z0-9_./:-]+")

        /** The single quotes of PowerShell: the ASCII one, and the four typographic ones, U+2018 to U+201B. */
        private val POWERSHELLS_SINGLE_QUOTES = setOf('\'', Char(0x2018), Char(0x2019), Char(0x201A), Char(0x201B))

        /**
         * The kind of shell that [shellCommand] runs: the command a terminal starts its shell with, the program first.
         * One it doesn't know is taken for a POSIX shell.
         */
        fun runBy(shellCommand: List<String>): Shell =
            when (shellCommand.first().substringAfterLast('\\').substringAfterLast('/').lowercase().removeSuffix(".exe")) {
                "pwsh" -> POWERSHELL
                "powershell" -> WINDOWS_POWERSHELL
                "cmd" -> CMD
                else -> POSIX
            }
    }
}

/**
 * How a Windows program splits the command line it's started with into words, as Windows' C runtime does, Claude Code
 * included: a word may be in double quotes, a `"` in it has a backslash before it, and the backslashes before such a
 * `"`, or before the closing one, are doubled.
 */
private object WindowsProgramWords {
    /** [word] in double quotes, as a program reads it back. */
    fun quoted(word: String): String = "\"" + trailingBackslashesDoubled(quotesEscaped(word)) + "\""

    /** [word] with a backslash before each `"` in it, and the backslashes before such a `"` doubled. */
    fun quotesEscaped(word: String): String = word.replace(Regex("""(\\*)"""")) { it.groupValues[1].repeat(2) + """\"""" }

    /** [text] with the backslashes it ends with doubled, as they must be before a closing `"`. */
    fun trailingBackslashesDoubled(text: String): String = text.replace(Regex("""\\+$""")) { it.value.repeat(2) }
}
