package dev.magnus.claudesessions.chat.transcript

/**
 * Which text the [TranscriptPane] shows of an item of the conversation: the [index]-th of the item [item]'s texts, by
 * its id, as [TranscriptTexts] lists them, such as a tool call's body, or a paragraph of what Claude said.
 */
data class ItemTextPart(val item: String, val index: Int)
