package dev.magnus.claudesessions.chat.claudecode

/**
 * What came of a message, or a key, that the chat gave the [ClaudeCodeBehindTheChat]: whether Claude Code got it, and
 * if not, why, and whether Claude Code's own UI, in the tab's terminal, shows what is in the way.
 */
sealed interface TypingOutcome {
    /** Why Claude Code didn't get it, a sentence for the user, or null when it did. */
    val whyNotTyped: String?

    /** Whether Claude Code's own UI, in the tab's terminal, shows what is in the way, or what was left there. */
    val theTerminalShowsWhy: Boolean

    /** Claude Code got it: the message was sent, as if typed into its own input box and Enter pressed, or the key pressed. */
    data object Typed : TypingOutcome {
        override val whyNotTyped: String? = null
        override val theTerminalShowsWhy = false
    }

    /**
     * Nothing of it was typed. When [theTerminalShowsWhy], what is in the way shows in Claude Code's own UI: a dialog it
     * shows, or text in its own input box.
     */
    data class NothingTyped(override val whyNotTyped: String, override val theTerminalShowsWhy: Boolean) : TypingOutcome

    /**
     * The message was typed into Claude Code's own input box, and is left there, unsent: the box showed something other
     * than what was typed, or Enter didn't send it. The tab's terminal shows it.
     */
    data class LeftUnsentInTheTerminal(override val whyNotTyped: String) : TypingOutcome {
        override val theTerminalShowsWhy = true
    }
}
