# How Claude Sessions works

Claude Sessions (plugin id `dev.magnus.claudesessions`, Kotlin package `dev.magnus.claudesessions`, Gradle project
`ClaudeSessions/`) is a tool window whose tabs each run a Claude Code session, and which resumes them when the IDE
restarts. It runs in any JetBrains IDE: it uses only the platform and the terminal plugin. Magnus asked for it on
2026-11-17: shutting down 3-8 IDEs with 1-4 Claude sessions each, and starting them all again by hand, was a real pain.

It lives in this repository as a project of its own, so that it can move to a repository of its own.

## A tab is a session

Each tab of the view (`ClaudeSessionsView`) is a reworked-terminal tab, made with the terminal's public
`TerminalToolWindowTabsManager.createTabBuilder()`, that runs `claude` itself, not a shell (`ClaudeCodeCommand` finds it
on the `PATH` as a shell would, `.cmd` scripts through `cmd.exe`). The tab closes when Claude Code exits. The terminal
plugin neither saves nor restores these tabs: `restoreOnProjectReopen(false)`.

## Which session a tab runs

Claude Code records each running session in `sessions/<process id>.json` in its folder (`CLAUDE_CONFIG_DIR`, or
`~/.claude`): the session id, the folder it runs in, and its name. It rewrites the record when `/clear` starts a new
session or `/resume` switches to another, and deletes it when it exits. `ClaudeCodeSessionsFolder` reads it for the
tab's process, which `TerminalView.startupOptionsDeferred` gives, or a process below it.

- **The record is undocumented internal state of Claude Code** (seen in 2.1.326). If a Claude Code update changes it,
  this is where it breaks. A `SessionStart` hook in Claude Code's settings would be the fallback.
- **A Claude Code that's killed leaves its record behind,** as when its tab closes, and a later process may get its
  process id: a record whose `startedAt` is before the process started is ignored.

## Saving and resuming

- **`ClaudeSessionKeeper`** saves the sessions the tabs run, in tab order, every two seconds, and once more as the
  project closes or the plugin unloads (`SavedClaudeSessions`, a file per project in the IDE's system folder). It starts
  only once the view has resumed the saved sessions, so that it never saves a view they aren't in yet.
- **`ClaudeSessionsStartup`** has the IDE give the tool window its content as the project opens, when sessions were
  saved, and the view resumes each with `claude --resume <id>`, in its own folder and tab.

## Surviving a reload of the plugin

Unloading the plugin, as for an update, removes its tool window, and with it the terminals of its tabs, which would end
every session mid-turn. So `ParkTheTabsWhileThePluginReloads` moves the tabs to the terminal's own Terminal tool window
first (`TabsParkedInTheTerminal`, with the terminal's public `detachTab` and `attachTab`), and the reloaded plugin takes
back each tab whose own process runs a saved session. Uninstalled, the sessions run on in the Terminal tool window.

A reference to the parked tabs can't be handed from the old plugin to the new: `Key.findKeyByName`, the one way to find
a value by name across class loaders, is deprecated. Finding them by the session their process runs needs no handover.

## The terminal API, and its traps

- **The reworked terminal's API is experimental** as a whole since 2025.3 (the SDK's "Embedded Terminal" page), so
  `verifyPlugin` lets experimental-API findings through, and fails on every other kind.
- **`TerminalToolWindowTabsManager.getTabs()` lists only the Terminal tool window's tabs:** the view keeps its own.
- **Its tab classes live in the content module `intellij.terminal.frontend`,** which a `<depends>` on
  `org.jetbrains.plugins.terminal` reaches, as the SDK says.
- **Closing a tab whose process runs asks the user to confirm,** both by its close button and by the API's `closeTab`,
  and so does exiting the IDE (`TerminalProcessesClosingNotifier`). The confirmation follows the IDE's "Process tabs:
  when closing with an active process" setting. Magnus's IDEs ask, as they do for `claude` in an ordinary terminal tab.
- **Kotlin's compatibility bridges** made each class implementing a platform interface override its default methods,
  deprecated ones included, which the verifier reports: `jvmDefault = NO_COMPATIBILITY` in the build.
