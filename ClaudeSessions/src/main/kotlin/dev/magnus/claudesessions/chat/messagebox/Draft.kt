package dev.magnus.claudesessions.chat.messagebox

import dev.magnus.claudesessions.chat.claudecode.Message

/**
 * What a [MessageBox] holds that the user hasn't sent: the [message] its text would be, each part as it came into the
 * box, typed, pasted or an image, and where its [caret] is, as an offset into the text. Saved as it changes, so that it
 * survives the IDE restarting or crashing.
 */
data class Draft(val message: Message, val caret: Int) {
    companion object {
        /** An empty box. */
        val NONE = Draft(Message(emptyList()), caret = 0)
    }
}
