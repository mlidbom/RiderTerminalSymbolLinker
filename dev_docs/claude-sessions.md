# How Claude Sessions works

Claude Sessions (plugin id `dev.magnus.claudesessions`, Kotlin package `dev.magnus.claudesessions`, Gradle project
`ClaudeSessions/`) is a tool window whose tabs each run a Claude Code session, and which resumes them when the IDE
restarts. It runs in any JetBrains IDE: it uses only the platform and the terminal plugin. Magnus asked for it on
2026-11-17: shutting down 3-8 IDEs with 1-4 Claude sessions each, and starting them all again by hand, was a real pain.

It lives in this repository as a project of its own, so that it can move to a repository of its own.

Its packages, below `dev.magnus.claudesessions`, which holds the tool window and its tabs:

- **`claudecode`:** Claude Code seen from outside: its sessions, the folder it keeps them in, its records of the
  sessions that run, its transcripts, and the command line a tab types to run it, quoted for the tab's shell.
- **`saving`:** saving the sessions the tabs run, and resuming them as the IDE starts.
- **`reload`:** keeping the tabs through a reload of the plugin.
- **`newsession`:** the + and the popup that names a new session.
- **`history`:** the session history picker.
- **`api`:** the local API through which other tools, and other IDEs, show a session's tab.
- **`observation`:** the development observation channel, which only the sandbox IDE runs.

## A tab runs a shell, and Claude Code in it

Each tab of the view (`ClaudeSessionsView`) is a reworked-terminal tab, made with the terminal's public
`TerminalToolWindowTabsManager.createTabBuilder()`, that runs the user's shell, into which the tab types `claude`,
`claude --name <name>`, or `claude --resume <id>` (`ClaudeCodeCommand`). When Claude Code exits, the shell is left at its prompt, and the tab keeps
the session that ran in it last; the tab closes when the shell exits. The terminal plugin neither saves nor restores
these tabs: `restoreOnProjectReopen(false)`.

- **Why a shell:** Magnus's session tooling reloads a session (to run a new Claude Code, or take in changed settings)
  by typing `/exit` into Claude Code, and then `claude --resume <id>` into the shell above it, which it takes to be
  Claude Code's direct parent process: pwsh, powershell, cmd, bash, sh or zsh (tooling-owner, 2026-11-17). The first
  version ran `claude` as the tab's own process, with no shell to type into, and its tabs closed with Claude Code.
- **So Claude Code must be the shell's direct child.** Typed into the shell, `claude.exe` is. A `cmd.exe /c` between
  them would be taken for the shell and exit with Claude Code. An npm install's `claude.cmd` runs that way, and such a
  session can't be reloaded.
- **The shell finds `claude` on its `PATH`,** as when a user types it. Where it doesn't, the shell says so in the tab.
- **Every terminal tab in the tool window is the view's,** not only those it opened: `ClaudeSessionsView.tabs` takes on
  any other as it comes across it, by the terminal's `TerminalToolWindowTab.KEY` on the content. A tab the view hadn't
  opened ran Magnus's resident-server session on 2026-11-17, and was neither saved nor kept through a reload. How it
  came into the tool window wasn't found: the terminal's New Tab action is disabled outside the Terminal tool window.

## Starting a new session, named or not

The **+** in the tool window's title bar (`NewClaudeSessionAction`) opens `NewClaudeSessionPopup` below it: a text field
for the session's name, as Magnus asked on 2026-11-19. Enter starts the session in a new tab, in the project's folder,
with `claude --name <name>`, which Claude Code (2.1.328) shows in its prompt box, its `/resume` picker and the terminal's
title, and the tab is named after it. With the field left empty, Enter starts it with plain `claude`, and the tab is
named "Claude". Escape, or a click outside, starts none.

**The tab types the name as its shell reads it** (`Shell`): any characters may be in a name, and each shell quotes
differently. The tab waits until its shell has started, and the terminal's startup options say which shell runs, before
it types. A word no shell treats specially, such as a session's id, is typed as it is.

- **PowerShell 7, `pwsh`,** Magnus's: single quotes, each single quote doubled, the typographic ones too.
- **Windows PowerShell, `powershell`:** single quotes too, but it hands a word with whitespace on to a program in double
  quotes without escaping a `"` in it, or a backslash before the closing one: the tab escapes them first, as Windows'
  C runtime reads them.
- **`cmd`:** double quotes, as Windows' C runtime reads them, and a `^` before each of cmd's own characters outside what
  it takes as quoted. cmd still puts a variable's value in place of `%NAME%`, and has no escape for that on its command
  line.
- **Any other shell** is taken for a POSIX one, such as bash: single quotes, each single quote ended, escaped and begun
  again.

The specifications start a session named with each character some shell acts on in each of these four, with Git Bash
for the POSIX one, and check that the fake Claude Code got the name as it was typed.

## Which session a tab runs

Claude Code records each running session in `sessions/<process id>.json` in its folder (`CLAUDE_CONFIG_DIR`, or
`~/.claude`): the session id, the folder it runs in, and its name. It rewrites the record when `/clear` starts a new
session or `/resume` switches to another, and deletes it when it exits. `ClaudeCodeSessionsFolder` reads it for the
processes below the tab's process, its shell, which `TerminalView.startupOptionsDeferred` gives
(`RunningClaudeCode.inOrBelow`).

- **The record is undocumented internal state of Claude Code** (seen in 2.1.326). If a Claude Code update changes it,
  this is where it breaks. A `SessionStart` hook in Claude Code's settings would be the fallback.
- **A Claude Code that's killed leaves its record behind,** as when its tab closes, and a later process may get its
  process id: a record whose `startedAt` is before the process started is ignored.

## Saving and resuming

- **`ClaudeSessionKeeper`** saves the sessions the tabs run, in tab order, and the selected tab's session
  (`ClaudeSessionsInTheView`), every two seconds, and once more as the project closes or the plugin unloads
  (`SavedClaudeSessions`, a file per project in the IDE's system folder). The view selects that tab again as it opens. It starts
  only once the view has resumed the saved sessions, so that it never saves a view they aren't in yet.
- **A tab whose Claude Code has exited keeps its session,** and the keeper saves it, so it's resumed at the next start.
  That way a stop of every session before an IDE restart, which the session tooling plans, ends with them resumed.
- **`ClaudeSessionsStartup`** has the IDE give the tool window its content as the project opens, when sessions were
  saved, and the view resumes each with `claude --resume <id>`, in its own folder and tab.
- **A session that can't be resumed is left out:** one whose transcript, `projects/<folder as a name>/<id>.jsonl` in
  Claude Code's folder, is gone (`ClaudeCodeTranscripts`), as archiving or deleting a session moves or removes it.
  `claude --resume` would fail on it.

## The session history picker

`ResumeClaudeSessionAction` (the history button in the tool window's title bar, and Ctrl+Alt+Shift+Y, free in the
default keymap, both Visual Studio keymaps and Magnus's own on 2026-11-18) opens `ClaudeSessionHistoryPicker`, a popup
over `ClaudeSessionHistory`: what Magnus asked for on 2026-11-18, with the changes he agreed to.

- **The history is the transcripts,** every `projects/*/<id>.jsonl`, read in the background as the picker opens.
  `ClaudeCodeTranscript.summary` reads only the last megabyte of each: Claude Code writes the session's names
  (`custom-title`, from `/rename`, and `ai-title`, its own) and its `last-prompt` again and again, and in Magnus's 67
  transcripts the last of each was at most 208 KB from the end, of files up to hundreds of megabytes. A session's
  folder is the `cwd` of its last `user` or `assistant` entry: a session resumed elsewhere goes on in the same file,
  and its entries name the new folder. Left out: a transcript with no such entry (no conversation), and one whose
  `entrypoint` there is an SDK's (`sdk-cli`, as `claude -p` writes). The read is logged to `idea.log` with its time.
- **Where each session is, `WhereTheSessionIs`, decides what choosing it does.** Running in a tab of this IDE: the tab
  is brought forward. Running in a tab of another IDE, as its local API's `GET /sessions` tells: that IDE is asked to
  show it, after this IDE, the active application, lets it come forward with Windows' `AllowSetForegroundWindow`,
  through the platform's JNA (`com.intellij.modules.jna`). Running elsewhere, as Claude Code's session records tell:
  greyed out, and it can't be chosen. Exited in a tab of this IDE: resumed in that tab. Otherwise: resumed in a new tab,
  in the project's folder, where a new session starts (Magnus's call), whichever folder it ran in: Claude Code 2.1.327
  finds the transcript in any project's folder, and goes on with it in the new one (tried with `claude -p --resume`).
- **The scopes** are the git worktree the project is in (or its folder, outside git), every worktree of its repository
  (`git worktree list`), and everything: `ProjectWorktrees` runs git.
- **The keys are plain Swing listeners,** the search field's for Up, Down and Enter, and the list's for Enter: the
  observation channel presses them through the same listeners, as no key event reaches an IDE whose window isn't
  focused. The platform's `ClickListener` counts clicks itself, so a double click is two clicks to it.

## Surviving a reload of the plugin

Unloading the plugin, as for an update, removes its tool window, and with it the terminals of its tabs, which would end
every session mid-turn. So `ParkTheTabsWhileThePluginReloads` moves every tab to the terminal's own Terminal tool window
first (`TabsParkedInTheTerminal`: the terminal's public `detachTab`, then the content manager's `addContent`), and the
reloaded plugin takes them all back, as they were, in their order. Uninstalled, the sessions run on in the Terminal tool
window.

- **A parked tab is marked by a client property of its component,** `dev.magnus.claudesessions.parkedTab`, holding the
  session it ran last. A property named by a string reaches the reloaded plugin, whose classes are new: a `Key` doesn't,
  and `Key.findKeyByName`, the one way to find one by name across class loaders, is deprecated.
- **The mark, not Claude Code's record, tells a tab apart.** Until 2026-11-17 the reloaded plugin took back the tabs
  whose Claude Code's record named a saved session, and parked only tabs in which Claude Code ran. One session of
  Magnus's, resident-server, wasn't taken back at any of four reloads, though four others in the same Rider were; its
  tab was resumed anew, or lost. Why its record didn't match was never found: the reload log below is there for the next
  time. A tab whose Claude Code had exited went with the tool window too.
- **Tabs parked by an earlier build carry no mark,** and are still taken back by the session their Claude Code runs.
- **Each park and take-back is logged** to the IDE's `idea.log` by `TabsParkedInTheTerminal`: each tab's name, process
  and session.
- **A known leak:** with tabs parked, the unloaded build stays in memory, and the IDE says the plugin "didn't unload
  fully" and advises a restart. With no tab open, the unload is clean. See the `bug:` comment in
  `TabsParkedInTheTerminal.park`. What it costs, measured in Magnus's Rider with 8 old builds in memory (2026-11-18):
  about 0.25 MB of metaspace for each old build's classes, and a few bytes of its objects; whatever holds them may hold
  more, which only a heap dump would show. None of an old build's code runs on: its views, keepers, coroutines and
  local API server were all gone.

## Whether a session can be ended without losing anything (researched, not built)

For item 27, asking before closing a tab or exiting the IDE when ending a session would lose something. Magnus's IDEs
never ask themselves (`processCloseConfirmation=TERMINATE`, `confirmExit=false`). tooling-owner read this from Claude
Code 2.1.327's own code and five transcripts on 2026-11-18. It isn't documented, so any release may change it.

- **The record's `status`,** rewritten with `statusUpdatedAt` as it or `waitingFor` changes:
  - `busy`: a turn, compaction, or a delegated task still running after the turn (a background subagent, a local
    workflow, a teammate that isn't idle, a remote agent that isn't long-running).
  - `waiting`: a dialog waits for the user. `waitingFor` names it: "permission prompt", "dialog open", "input needed",
    "sandbox request", "worker request", "goal proposal".
  - `shell`: otherwise idle, with a background Bash task or a Monitor running.
  - `idle`: none of those.
- **What `idle` misses:** an armed wake-up; MCP monitors and tasks; a long-running remote agent's result; a draft in
  the input box, which Claude Code doesn't keep; the turn's last lines, which land within 0.1 s after `idle`, so it
  must stand for 0.5 s; a crashed process's record, so the process must be alive with the record's `procStart`;
  processes the session started detached.
- **Armed wake-ups, from the transcript:** read `user` and `assistant` entries only (`api-request-blob` repeats the
  calls), and only those whose `sessionId` is the session's own (a fork carries the original's). A call is a `tool_use`
  in an assistant entry, and its result the `toolUseResult` of the next user entry with that `tool_use_id`.
  - `ScheduleWakeup` arms with a result `{scheduledFor, clampedDelaySeconds, wasClamped}`, and with input
    `{"stop": true}` cancels: `{"scheduledFor": 0, "stopped": true, "cancelledWakeups": n}`. Whether a second one
    replaces the first isn't verified.
  - `CronCreate` with `"durable": false` arms a job that dies with the process (`{id, humanSchedule, recurring, durable,
    nextFireAt}`). Durable jobs are kept in `.claude/scheduled_tasks.json`. `CronDelete {id}` cancels. A one-shot job
    deletes itself once it fires.
  - A fire starts a turn whose user entry has `"turnOrigin": "scheduled"` (from the code, never seen). Failing that, a
    wake-up whose time passed while the process lived has fired.
  - Only calls since the current process started count, by the entry's timestamp against the record's `startedAt`: a
    resume lost resident-server's session-only cron job on 2026-11-17. Whether a resume re-arms a /loop wake-up isn't
    known: 2.1.327 has code that rebuilds one from the transcript.

## The local API: showing a session's tab for another tool

Clicking the notifications of Magnus's session tooling (tooling-owner's, in `~/.claude`) brings forward the IDE window
of the session, and then asks Claude Sessions in that IDE to show the session's tab. The contract, agreed with
tooling-owner on 2026-11-17 (protocol 1):

- **`LocalApi`,** an application service that the first project's `ClaudeSessionsStartup` starts, listens on a free
  port of the loopback address. **`LocalApiAnnouncement`** names it in
  `%LOCALAPPDATA%\ClaudeSessions\ides\<the IDE's process id>.json`, `{"protocol": 1, "processId": …, "port": …}`,
  and deletes the file as the API stops. The tooling knows the IDE's process: the one that owns the window it brought
  forward.
- **`POST /sessions/show?session=<id>&process=<Claude Code's process id>`** selects the tab in which that Claude Code
  runs, or else the one that runs the session or ran it last, shows the tool window, and brings the project's window in
  front of the IDE's others: 200 `{"shown": true}`, or 404 `{"shown": false}`. Windows lets the window come forward
  when the IDE is the active application, or the active one allowed it, and otherwise flashes its taskbar button.
  `process` finds a tab after `/clear` gave it a new session, as the notification names the old one; `session` finds a
  tab whose Claude Code was resumed in a new process.
- **`GET /sessions`** lists the sessions that run in the IDE's tabs, `{"sessions": [{"session": <id>, "process":
  <Claude Code's process id>}]}`: the session history picker of another IDE asks (2026-11-18).
- **The tooling** brings the window forward itself, since Windows lets the process the user clicked do that, and not
  the IDE. It asks the window's own IDE first, then every other announced one, a second each, and logs the answer to
  `~/.claude/notifying-magnus.log`.
- **No authentication,** as showing a tab is harmless. tooling-owner's condition: a request that changes something,
  such as resuming a session or typing into one, takes a token in the announcement, required with each request.
- **A file left by a crash** costs a click up to the tooling's one-second timeout: on Windows, a connection to a loopback
  port nothing listens on takes about two seconds to be refused.

## The terminal API, and its traps

- **The reworked terminal's API is experimental** as a whole since 2025.3 (the SDK's "Embedded Terminal" page), so
  `verifyPlugin` lets experimental-API findings through, and fails on every other kind.
- **`TerminalToolWindowTabsManager.getTabs()` lists only the Terminal tool window's tabs:** the view keeps its own.
- **`attachTab` activates the Terminal tool window,** whichever content manager it attaches the tab to, and selects
  the tab only once that's active. The Terminal tool window shares the bottom of the IDE with the view's, so a reload
  left it shown in the view's place, and the last tab taken back selected whatever had been. A tab is moved with
  `detachTab`, which removes its content without ending its session, and `addContent`. Only the terminal's own tips,
  migration notices and tab persistence hear of an attached tab, which these tabs want none of.
- **Its tab classes live in the content module `intellij.terminal.frontend`,** which a `<depends>` on
  `org.jetbrains.plugins.terminal` reaches, as the SDK says.
- **A tab whose shell starts before the tab is laid out can keep 80 columns by 20 rows,** and Claude Code drawn at
  that size, until the tab's size changes. With `deferSessionStartUntilUiShown(false)` the terminal starts the shell at
  80 by 20, its guess; it drops a resize that comes before the shell has started, and sends none once it has. So a tab
  shown at once, opened by the + or by the history picker, starts its shell only once it's laid out, at its size. A tab
  resumed as the IDE starts starts its shell at once, so that its session runs though its tab is never shown, and gets
  its size as it's first shown. The one the view selects then, shown at once when the tool window is, can keep 80 by
  20: seen in the sandbox on 2026-11-19, and not fixed (the `bug:` at `ClaudeSessionsView.resume`). The same in Rider
  2026.3 (263.7307) and in intellij-community's code on 2026-11-19.
- **A banner above a tab takes rows from it without resizing its shell** in Rider 2026.3: the terminal resizes the
  shell only as its whole panel's size changes. The MCP Server plugin shows one, asking to enable the IDE's MCP server,
  when `claude` starts in a tab of an IDE whose MCP server isn't enabled: Claude Code then draws three rows taller than
  the tab shows. intellij-community's code resizes the shell for it since.
- **Closing a tab whose process runs asks the user to confirm,** both by its close button and by the API's `closeTab`,
  and so does exiting the IDE (`TerminalProcessesClosingNotifier`). The confirmation follows the IDE's "Process tabs:
  when closing with an active process" setting. Magnus's IDEs ask, as they do for `claude` in an ordinary terminal tab.
- **`doNotActivateOnStart` on the tool window hides it at every start,** even when it was open as the IDE closed: the
  platform sets it invisible as it reads the saved layout (`WindowInfoImpl.normalizeAfterRead`, 2026.3). Magnus's
  IntelliJ IDEA did so with the first version. The sandbox Rider showed it all the same, so its specification of the
  view shown again after a restart passed with the attribute too.
- **Kotlin's compatibility bridges** made each class implementing a platform interface override its default methods,
  deprecated ones included, which the verifier reports: `jvmDefault = NO_COMPATIBILITY` in the build.
