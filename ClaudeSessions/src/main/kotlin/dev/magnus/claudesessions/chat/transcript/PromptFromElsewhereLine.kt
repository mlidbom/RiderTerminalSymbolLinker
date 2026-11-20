package dev.magnus.claudesessions.chat.transcript

import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere

/**
 * The one line a [PromptFromElsewhere] shows as in the [TranscriptPane] while it's closed: where it came from, its
 * [PromptFromElsewhereCaption], and what it's about, such as `From tooling-owner · Yes, through the hub: one restart`.
 * What a background task's notice is about is its summary, such as `Agent "Fix the build" finished`, which Claude Code's
 * notice holds among the task's details; what any other prompt is about, its first line.
 */
object PromptFromElsewhereLine {
    private val SUMMARY = Regex("<summary>(.*?)</summary>", RegexOption.DOT_MATCHES_ALL)

    /** The line [prompt] shows as. */
    fun of(prompt: PromptFromElsewhere): String =
        listOf(PromptFromElsewhereCaption.of(prompt.from), whatItIsAbout(prompt.text)).filter { it.isNotEmpty() }.joinToString(" · ")

    private fun whatItIsAbout(text: String): String =
        SUMMARY.find(text)?.groupValues?.get(1)?.trim() ?: text.lineSequence().map(String::trim).firstOrNull(String::isNotEmpty).orEmpty()
}
