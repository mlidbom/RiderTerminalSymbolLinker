package dev.magnus.claudesessions.observation

import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChat
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChatProvider

/**
 * Gives each tab a [StandInBehindTheChat], in the sandbox IDE only, where the [ObservationChannel] listens, unless the
 * specifications asked for the hub behind the chats ([standInsBehindTheChats]): anywhere else, none, so that the hub's
 * adapter gives the tab one.
 */
class StandInBehindTheChatInTheSandbox : ClaudeCodeBehindTheChatProvider {
    override fun behindTheChatOf(tab: ClaudeSessionTab): ClaudeCodeBehindTheChat? = if (ObservationChannel.listens && standInsBehindTheChats) StandInBehindTheChat() else null

    companion object {
        /** Whether the tabs opened from now on get a stand-in behind their chats, as they do unless a story asks otherwise. */
        @Volatile
        var standInsBehindTheChats = true
    }
}
