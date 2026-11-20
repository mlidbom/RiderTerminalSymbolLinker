package dev.magnus.claudesessions.chat.transcript

import dev.magnus.claudesessions.chat.claudecode.conversation.PromptFromElsewhere
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/** The one line a prompt from elsewhere shows as while it's closed: where it came from, and what it's about. */
class PromptFromElsewhereLineTest {
    @Test
    fun `another session's message is about its first line that holds text`() {
        val message = PromptFromElsewhere("m1", PromptFromElsewhere.Source.AnotherSession("tooling-owner"), "\n  Published (2da6bcd).  \nThe rest.")

        assertEquals("From tooling-owner · Published (2da6bcd).", PromptFromElsewhereLine.of(message))
    }

    @Test
    fun `a background task's notice is about its summary`() {
        val notice = PromptFromElsewhere(
            "b1",
            PromptFromElsewhere.Source.BackgroundTask,
            "<task-notification>\n<task-id>aba631c</task-id>\n<summary>Agent \"Fix three typing bugs\" finished</summary>\n<result>Done.</result>\n</task-notification>",
        )

        assertEquals("A background task finished · Agent \"Fix three typing bugs\" finished", PromptFromElsewhereLine.of(notice))
    }

    @Test
    fun `an empty prompt is only where it came from`() {
        assertEquals("A scheduled wake-up", PromptFromElsewhereLine.of(PromptFromElsewhere("w1", PromptFromElsewhere.Source.ScheduledWakeUp, "")))
    }
}
