package dev.magnus.claudesessions.claudecode

/**
 * What a running Claude Code last showed under its input box, in its status line and the line below it: its model, its
 * effort level, how much of its context window is used, and its permission mode. The Claude tooling's hub keeps them,
 * from what Claude Code gives the status line's command, and from its screen.
 *
 * The chat shows them under its own input box, as the desktop app's "Opus 5.5 · High · Auto". Magnus, 2026-11-20: "the
 * stuff below it should come from the current status line shown in the TUI".
 */
data class ClaudeCodesStatusLine(
    /** The model, as Claude Code names it, such as "Opus 5.5". */
    val model: String,
    /** The effort level, as Claude Code names it, such as `xhigh`: null on a model with none. */
    val effortLevel: String?,
    /** How much of the context window is used, in percent: null before the session's first reply. */
    val contextUsedPercentage: Int?,
    /** Null until the hub has read it off Claude Code's screen. */
    val permissionMode: PermissionMode?,
) {
    /** What it says in one line: `Opus 5.5 · xhigh · Auto · context 31%`, leaving out what isn't known. */
    val text: String
        get() = listOfNotNull(model, effortLevel, permissionMode?.inShort, contextUsedPercentage?.let { "context $it%" }).joinToString(" · ")

    /**
     * A permission mode of Claude Code's: as its settings name it, such as `auto`, which is null for a mode the hub
     * doesn't know, and as its screen shows it, such as "auto mode on".
     */
    data class PermissionMode(val settingsName: String?, val shownAs: String) {
        /** The mode in a word or two, as the desktop app's footer names it, such as "Auto": as shown, for a mode the hub doesn't know. */
        val inShort: String
            get() = when (settingsName) {
                "bypassPermissions" -> "Bypass permissions"
                "auto" -> "Auto"
                "default" -> "Manual"
                "acceptEdits" -> "Accept edits"
                "plan" -> "Plan"
                else -> shownAs
            }
    }
}
