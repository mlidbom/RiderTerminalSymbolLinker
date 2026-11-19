package dev.magnus.claudesessions.chat.claudecode

import com.intellij.openapi.extensions.ExtensionPointName
import dev.magnus.claudesessions.ClaudeSessionTab

/**
 * Gives a [ClaudeSessionTab] the [ClaudeCodeBehindTheChat] its chat talks to: the extension point
 * `dev.magnus.claudesessions.claudeCodeBehindTheChat`, through which the source of the conversation, and of the way to
 * type into Claude Code, can switch. A tab that no provider gives one has no chat, and shows its terminal alone, as
 * before the chat existed.
 *
 * Today only the sandbox IDE's stand-in provides one: which side will provide it for Magnus's IDEs is being agreed (see
 * [ClaudeCodeBehindTheChat]).
 */
interface ClaudeCodeBehindTheChatProvider {
    /** The Claude Code behind [tab]'s chat, new, or null when this provider has none for it. */
    fun behindTheChatOf(tab: ClaudeSessionTab): ClaudeCodeBehindTheChat?

    companion object {
        private val EXTENSION_POINT = ExtensionPointName<ClaudeCodeBehindTheChatProvider>("dev.magnus.claudesessions.claudeCodeBehindTheChat")

        /** The Claude Code behind [tab]'s chat, from the first provider that has one, or null when none has. */
        fun behindTheChatOf(tab: ClaudeSessionTab): ClaudeCodeBehindTheChat? =
            EXTENSION_POINT.extensionList.firstNotNullOfOrNull { it.behindTheChatOf(tab) }
    }
}
