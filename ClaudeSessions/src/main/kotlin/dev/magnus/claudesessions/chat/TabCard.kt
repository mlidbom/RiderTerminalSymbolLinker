package dev.magnus.claudesessions.chat

/** One of the two cards of [ChatAndTerminalCards], which a tab shows one at a time. */
enum class TabCard {
    /** The tab's [Chat]. */
    CHAT,

    /** The tab's terminal, with Claude Code's own UI in it. */
    TERMINAL;

    /** The card the tab doesn't show while it shows this one. */
    val other: TabCard get() = if (this == CHAT) TERMINAL else CHAT
}
