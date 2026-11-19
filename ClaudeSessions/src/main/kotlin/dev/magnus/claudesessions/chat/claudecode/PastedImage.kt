package dev.magnus.claudesessions.chat.claudecode

import java.nio.file.Path

/**
 * An image the user pasted into the input box from the clipboard, as the PNG [file] the chat saved it to: Claude Code
 * attaches an image whose file's path is pasted into its box by itself.
 *
 * @property text The place-holder the input box shows for it, such as `[Image #1]`.
 */
data class PastedImage(val file: Path, override val text: String) : MessagePart
