package dev.magnus.claudesessions.hub

import com.google.gson.JsonObject
import com.google.gson.JsonParser
import dev.magnus.claudesessions.chat.claudecode.conversation.BlockBeingWritten

/**
 * The hub's stream of the text the model streams in a running session's turn, `GET
 * /running-sessions/{processId}/turn/stream`, read event by event into the block being written, as its contract says
 * (`~/.claude/dev_docs/the-hubs-local-api.md`, "The turn under way"): each `piece` added to its block, or beginning the
 * next, each `block-ended` marking it ended, and each `turn-ended` taking away a block that hadn't. From the `caught-up`
 * on, it tells of each change.
 *
 * An ended block is kept, with [StreamedBlock.hasEnded], until the next begins: Claude Code has written it to the
 * transcript, and the conversation's stream sends it a moment later, so whoever shows it keeps showing it until then.
 */
class TheHubsTurnStream {
    /** The block written last, or null while none is. */
    var streamed: StreamedBlock? = null
        private set

    private var caughtUp = false

    /** Reads [event], and returns whether [streamed] is to be told of: once the stream has caught up, and it may have changed. */
    fun read(event: ServerSentEvent): Boolean {
        when (event.name) {
            "piece" -> readThePiece(JsonParser.parseString(event.data).asJsonObject)
            "block-ended" -> {
                val ended = JsonParser.parseString(event.data).asJsonObject
                streamed = streamed?.takeIf { it.block.id == idOf(ended) }?.copy(hasEnded = true) ?: streamed
            }
            "turn-ended" -> streamed = streamed?.takeIf { it.hasEnded }
            "caught-up" -> caughtUp = true
            else -> return false
        }
        return caughtUp
    }

    /** Adds [piece] to the block it goes on, or begins its block with it; a piece of a kind this build doesn't know is left out. */
    private fun readThePiece(piece: JsonObject) {
        val kind = when (piece.get("kind").asString) {
            "text" -> BlockBeingWritten.Kind.TEXT
            "thinking" -> BlockBeingWritten.Kind.THINKING
            else -> return
        }
        val id = idOf(piece)
        val text = piece.get("text").asString
        val goesOn = streamed?.takeIf { it.block.id == id && !it.hasEnded }
        streamed = goesOn?.copy(block = goesOn.block.copy(text = goesOn.block.text + text)) ?: StreamedBlock(BlockBeingWritten(id, kind, text), hasEnded = false)
    }

    /** The id of the block [event] tells of: its turn's, its response's and its own. */
    private fun idOf(event: JsonObject): String = "being-written:${event.get("turnId").asString}:${event.get("response").asInt}:${event.get("block").asInt}"

    /** A block the model writes, or wrote: [hasEnded] once Claude Code has written it to the transcript. */
    data class StreamedBlock(val block: BlockBeingWritten, val hasEnded: Boolean)
}
