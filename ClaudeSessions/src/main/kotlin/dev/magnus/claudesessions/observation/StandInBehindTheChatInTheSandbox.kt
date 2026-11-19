package dev.magnus.claudesessions.observation

import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChat
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChatProvider

/**
 * Gives each tab a [StandInBehindTheChat], in the sandbox IDE only, where the [ObservationChannel] listens: anywhere
 * else, none, so that a tab shows its terminal alone.
 */
class StandInBehindTheChatInTheSandbox : ClaudeCodeBehindTheChatProvider {
    override fun behindTheChatOf(tab: ClaudeSessionTab): ClaudeCodeBehindTheChat? = if (ObservationChannel.listens) StandInBehindTheChat() else null
}
