package dev.magnus.claudesessions.chat.claudecode.conversation

/**
 * A prompt Claude got that the user didn't give: a message from another session, a background task's notice that it
 * finished, a scheduled wake-up, Claude Code's own notice, or a program's prompt, such as `claude -p`'s. [from] says
 * which; [text] is the prompt as sent.
 */
data class PromptFromElsewhere(override val id: String, val from: Source, val text: String) : ConversationItem {
    /** Where a [PromptFromElsewhere] came from. */
    sealed interface Source {
        /** Another session, by Claude Code's `SendMessage`: [name] is its name, or null when it has none. */
        data class AnotherSession(val name: String?) : Source

        /** A shell command or an agent Claude ran in the background, which finished. */
        data object BackgroundTask : Source

        /** A wake-up Claude scheduled for itself. */
        data object ScheduledWakeUp : Source

        /** Claude Code itself, as its notice that a session asked about is idle. */
        data object ClaudeCode : Source

        /** A program, as `claude -p` or the Agent SDK. */
        data object Program : Source
    }
}
