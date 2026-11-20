package dev.magnus.claudesessions.chat.transcript

import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere

/** What the [TranscriptPane] says above a [PromptFromElsewhere] of where it came from, such as `From deskmancer`. */
object PromptFromElsewhereCaption {
    /** The caption of a prompt from [source]. */
    fun of(source: PromptFromElsewhere.Source): String = when (source) {
        is PromptFromElsewhere.Source.AnotherSession -> "From ${source.name ?: "another session"}"
        PromptFromElsewhere.Source.BackgroundTask -> "A background task finished"
        PromptFromElsewhere.Source.ScheduledWakeUp -> "A scheduled wake-up"
        PromptFromElsewhere.Source.ClaudeCode -> "From Claude Code"
        PromptFromElsewhere.Source.Program -> "From a program"
    }
}
