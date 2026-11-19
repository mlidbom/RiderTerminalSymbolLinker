package dev.magnus.claudesessions.chat.saving

import com.intellij.openapi.Disposable
import com.intellij.openapi.application.EDT
import com.intellij.openapi.diagnostic.thisLogger
import dev.magnus.claudesessions.sessionhosts.SessionHostId
import java.util.concurrent.Executors
import java.util.concurrent.Future
import kotlin.time.Duration.Companion.milliseconds
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/**
 * Keeps a tab's [SavedChat] current, in [savedChats], as the chat of the tab whose session runs in [savedAs]: half a
 * second after the chat last [changed], so that a crash loses at most that, and at once as the tab is detached from its
 * session, as the IDE exits or the plugin unloads ([saveNow]). [chatAsItIs] says what the chat is.
 *
 * The files are written on a thread of its own, one after another, in the order they were saved.
 *
 * Its methods run on the EDT.
 */
class ChatKeeper(private val savedChats: SavedChats, private val savedAs: SessionHostId, private val chatAsItIs: () -> SavedChat) : Disposable {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.EDT)
    private val writer = Executors.newSingleThreadExecutor { Thread(it, "Claude Sessions: saving the chat of session host ${savedAs.value}").apply { isDaemon = true } }

    /** The save that waits for the chat to stop changing, if any. */
    private var saving: Job? = null

    /** The chat changed: it's saved once it hasn't changed for [AFTER_THE_LAST_CHANGE]. */
    fun changed() {
        saving?.cancel()
        saving = scope.launch {
            delay(AFTER_THE_LAST_CHANGE)
            val chat = chatAsItIs()
            inTheWriter { savedChats.write(savedAs, chat) }
        }
    }

    /** Saves the chat as it is, and returns once it's written. */
    fun saveNow() {
        saving?.cancel()
        val chat = chatAsItIs()
        inTheWriter { savedChats.write(savedAs, chat) }.get()
    }

    /** Forgets the saved chat, as the user closed the tab, and returns once it's gone. */
    fun forget() {
        saving?.cancel()
        inTheWriter { savedChats.delete(savedAs) }.get()
    }

    /**
     * Has the writer do [write] after what it was given before. A failure is reported in the log: it ends that save
     * alone, and the next is written as ever.
     */
    private fun inTheWriter(write: () -> Unit): Future<*> = writer.submit {
        try {
            write()
        } catch (failure: Exception) {
            thisLogger().error("Saving the chat of session host ${savedAs.value} failed", failure)
        }
    }

    override fun dispose() {
        scope.cancel()
        writer.shutdown()
    }

    private companion object {
        /** How long after the chat last changed it's saved: as the user types, the box changes at every key. */
        val AFTER_THE_LAST_CHANGE = 500.milliseconds
    }
}
