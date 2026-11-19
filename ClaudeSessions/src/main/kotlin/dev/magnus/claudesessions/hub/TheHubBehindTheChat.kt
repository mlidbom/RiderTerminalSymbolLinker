package dev.magnus.claudesessions.hub

import dev.magnus.claudesessions.ClaudeSessionTab
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeBehindTheChat
import dev.magnus.claudesessions.chat.claudecode.ClaudeCodeStatus
import dev.magnus.claudesessions.chat.claudecode.Message
import dev.magnus.claudesessions.chat.claudecode.TypingOutcome
import dev.magnus.claudesessions.chat.claudecode.conversation.Conversation
import dev.magnus.claudesessions.claudecode.RunningClaudeCode
import dev.magnus.claudesessions.claudecode.RunningClaudeCodes
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn

/**
 * The Claude Code behind the chat of [tab], through the [ClaudeToolingHub]: the status of the Claude Code that runs in
 * the tab, as the hub tells of it, and the chat's messages and keys, which the hub types into it. The plugin's adapter
 * to the hub, as agreed (`~/.claude/dev_docs/the-claude-tooling-bundle.md`), which takes each of the hub's steps as it
 * lands.
 *
 * wip: The conversation is none until the hub tells of a session's conversation, the bundle's next step; until then no
 * provider gives a tab one, as a chat that shows no conversation is of no use.
 */
class TheHubBehindTheChat(private val tab: ClaudeSessionTab) : ClaudeCodeBehindTheChat {
    private val hub = ClaudeToolingHub.getInstance()
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)

    override val conversation: StateFlow<Conversation?> = MutableStateFlow<Conversation?>(null).asStateFlow()

    override val status: StateFlow<ClaudeCodeStatus> =
        hub.runningClaudeCodes.map(::statusInTheTab).stateIn(scope, SharingStarted.Eagerly, statusInTheTab(hub.runningClaudeCodes.value))

    override suspend fun send(message: Message): TypingOutcome = typedIntoTheTabsClaudeCode { hub.typing.send(it, message) }

    override suspend fun interrupt(): TypingOutcome = typedIntoTheTabsClaudeCode(hub.typing::interrupt)

    override suspend fun changeThePermissionMode(): TypingOutcome = typedIntoTheTabsClaudeCode(hub.typing::changeThePermissionMode)

    private suspend fun typedIntoTheTabsClaudeCode(type: suspend (RunningClaudeCode) -> TypingAnswer): TypingOutcome {
        val claudeCode = tab.claudeCode ?: return TypingOutcome.NothingTyped(whyNotTyped = "No Claude Code runs in the tab.", theTerminalShowsWhy = true)
        return outcomeOf(type(claudeCode))
    }

    private fun statusInTheTab(running: RunningClaudeCodes): ClaudeCodeStatus =
        when (tab.claudeCodeAmong(running)?.status) {
            null -> ClaudeCodeStatus.NOT_RUNNING
            RunningClaudeCode.Status.IDLE, RunningClaudeCode.Status.IDLE_WITH_A_BACKGROUND_TASK -> ClaudeCodeStatus.IDLE
            RunningClaudeCode.Status.BUSY, RunningClaudeCode.Status.OTHER -> ClaudeCodeStatus.BUSY
            RunningClaudeCode.Status.WAITING -> ClaudeCodeStatus.WAITING
        }

    override fun dispose() {
        scope.cancel()
    }

    internal companion object {
        /** Typed into Claude Code's own input box, and left there, unsent. */
        private val LEFT_UNSENT = setOf("inputBoxShowsOtherThanTyped", "notSentByEnter")

        /** Nothing typed, for what Claude Code's own UI shows: a dialog in place of its input box, or text in it. */
        private val FOR_WHAT_THE_TERMINAL_SHOWS = setOf("noInputBox", "inputBoxHoldsText")

        /**
         * What came of typing into Claude Code, as the hub's [answer] says: a reason this build doesn't know, as the contract
         * has it, shows the hub's sentence alone.
         */
        fun outcomeOf(answer: TypingAnswer): TypingOutcome = when (answer) {
            TypingAnswer.Typed -> TypingOutcome.Typed
            is TypingAnswer.NotTyped -> when (answer.reason) {
                in LEFT_UNSENT -> TypingOutcome.LeftUnsentInTheTerminal(answer.why)
                else -> TypingOutcome.NothingTyped(answer.why, theTerminalShowsWhy = answer.reason in FOR_WHAT_THE_TERMINAL_SHOWS)
            }
        }
    }
}
