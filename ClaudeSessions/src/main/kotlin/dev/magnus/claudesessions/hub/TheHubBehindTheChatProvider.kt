package dev.magnus.claudesessions.hub

import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChat
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChatProvider

/** Gives each tab whose session runs in a session host [TheHubBehindTheChat]: the hub tells of its session, and types into it. */
class TheHubBehindTheChatProvider : ClaudeCodeBehindTheChatProvider {
    override fun behindTheChatOf(tab: ClaudeSessionTab): ClaudeCodeBehindTheChat? = if (tab.sessionHost == null) null else TheHubBehindTheChat(tab)
}
