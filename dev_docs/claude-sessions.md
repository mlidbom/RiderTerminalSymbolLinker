# How Claude Sessions works

Claude Sessions (plugin id `dev.magnus.claudesessions`, Kotlin package `dev.magnus.claudesessions`, Gradle project
`ClaudeSessions/`) is a tool window whose tabs each run a Claude Code session, and which resumes them when the IDE
restarts. It runs in any JetBrains IDE: it uses only the platform and the terminal plugin. Magnus asked for it on
2026-11-17: shutting down 3-8 IDEs with 1-4 Claude sessions each, and starting them all again by hand, was a real pain.

It lives in this repository as a project of its own. **Decided, not yet done (Magnus, 2026-11-19):** Claude Sessions and
the symbol linker become one plugin, a Claude Code enhancement plugin, under the symbol linker's id,
`dev.magnus.riderterminalsymbollinker`, with a new name, so that the symbol linker's users get it as an update. The
symbol linking becomes an optional part, in a config file of its own that loads only when the .NET
Analysis&Refactoring MCP plugin is installed, and the plugin runs in every JetBrains IDE. Its name is Cloyd (Magnus,
2026-11-19), which plays on Claude as Royder, the MCP plugin's coming name, plays on Rider. Renaming the GitHub
repository is still Magnus's to choose. The merge comes after the chat view's first version (below).

Its packages, below `dev.magnus.claudesessions`, which holds the tool window and its tabs:

- **`claudecode`:** Claude Code seen from outside: its sessions, the folder it keeps them in, the Claude Codes that
  run, its transcripts, and the command a tab's shell runs it with.
- **`hub`:** the Claude tooling's hub, as the plugin follows it and types through it: the one place that knows the hub's
  local API.
- **`saving`:** saving the sessions the tabs run, and resuming them as the IDE starts.
- **`reload`:** keeping the tabs through a reload of the plugin.
- **`newsession`:** the + and the popup that names a new session.
- **`history`:** the session history picker.
- **`api`:** the local API through which other tools, and other IDEs, show a session's tab.
- **`chat`:** the chat view: a tab's two cards, the chat and the terminal, the chat itself, and the switch between
  them. Below it, `chat.transcript`, the pane that shows the conversation; `chat.messagebox`, the box the user writes
  in; and `chat.claudecode`, what the chat needs of the Claude Code behind it, with `chat.claudecode.conversation`, the
  conversation as the chat shows it.
- **`observation`:** the development observation channel, which only the sandbox IDE runs.

## A tab runs a shell, and Claude Code in it

Each tab of the view (`ClaudeSessionsView`) is a reworked-terminal tab, made with the terminal's public
`TerminalToolWindowTabsManager.createTabBuilder()`, that runs the user's shell. Once the shell has started, the hub types
`claude`, `claude --name <name>`, or `claude --resume <id>` into it (`ClaudeCodeCommand`), at the tab's request: the hub
owns all typing into Claude Code and the shell under it (below, "Who provides what"). When Claude Code exits, the shell is
left at its prompt, and the tab keeps the session that ran in it last; the tab closes when the shell exits. The terminal
plugin neither saves nor restores these tabs: `restoreOnProjectReopen(false)`.

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

**The hub types the name as the tab's shell reads it** (`POST /shells/{processId}/start-claude-code`): any characters may
be in a name, and each shell quotes differently. The tab asks once its shell has started. The quoting rules for pwsh,
Windows PowerShell, cmd and POSIX shells were the plugin's until 2026-11-19, and are the hub's now, in its contract,
"Typing into a running session". When the hub doesn't type the line, the tab's shell waits at its prompt, and a
notification says why.

The specifications start a session named with each character some shell acts on in each of these four, with Git Bash
for the POSIX one, and check that the fake Claude Code got the name as it was typed: the plugin and the hub, end to end.

## Which session a tab runs: the hub tells

Claude Code records each running session in `sessions/<process id>.json` in its folder (`CLAUDE_CONFIG_DIR`, or
`~/.claude`): the session id, the folder it runs in, its name and its status. It rewrites the record when `/clear`
starts a new session or `/resume` switches to another, and deletes it when it exits. **The plugin doesn't read the
records:** the Claude tooling's hub does, ClaudeTooling's resident server, which owns everything about Claude Code, as
agreed (below, "Who provides what"), and the plugin follows its stream of them. A tab's Claude Code is the one the hub
tells of below the tab's process, its shell, which `TerminalView.startupOptionsDeferred` gives
(`RunningClaudeCodes.inOrBelow`).

- **`ClaudeToolingHub`,** an application service the first project's `ClaudeSessionsStartup` starts, follows
  `GET /running-sessions/stream` of the hub's local API, whose contract is `~/.claude/dev_docs/the-hubs-local-api.md`,
  and keeps what it last told as a `StateFlow` of `RunningClaudeCodes`. It finds the hub by its announcement,
  `%LOCALAPPDATA%\ClaudeTooling\local-api.json` (`HubAnnouncement`), or in the folder `CLAUDE_TOOLING_LOCAL_API_FOLDER`
  names, as the sandbox's does. Each request carries the announcement's token.
- **A new hub at each publish:** the tooling is republished several times a day, under running sessions. The old hub
  ends its stream and stops, and the new build's announces itself, on a port and with a token of its own, a few tenths
  of a second later. So when a stream ends, or the hub announced isn't there, the plugin reads the announcement again,
  every quarter of a second, and meanwhile keeps what the last hub told, leaving out the Claude Codes whose processes
  have exited since.
- **The stream is read on a thread it blocks, interruptibly** (`runInterruptible`): a plugin's unload cancels its
  services' coroutine scopes and waits for them. Read without the interrupt, the stream kept its coroutine running,
  and the sandbox IDE froze in the unload (2026-11-19).
- **No hub, no Claude Code:** every Claude Code session's SessionStart hook starts the hub, which stops only once no
  session has run for 5 minutes. But a tab's shell must have Claude Code typed into it first, as when the IDE resumes
  its sessions after a reboot: then the plugin starts the published hub, with `~/.claude/bin/ClaudeTooling.exe
  resident-server start` (`ThePublishedHub`), and waits for it to announce itself. A hub tried out apart from the
  published one, as the sandbox's, is started by whoever tries it.
- **Typing through the hub** (`TypingIntoClaudeCode`): each request is answered once typed, which may take minutes
  while the tooling's own typing has the session's turn, or refused with why. At a publish, a request whose hub stops
  goes to the next one.
- **A protocol this build doesn't know:** the hub raises its protocol's version, in the announcement and in each
  answer's header, only for a change a reader of the earlier one would get wrong. The plugin then tells the user, once
  for each hub, in a notification, to restart the IDE, rather than read it wrongly. A hub of another Claude Code folder
  than the IDE's is left alone, with a line in `idea.log`.
- **The contract test,** `TheHubsLocalApiContractTest`, reads copies of the hub's samples of its answers, which its own
  specifications compare its answers with.
- **The record is undocumented internal state of Claude Code,** which the hub follows as each version writes it. Its
  status, `idle`, `busy`, `waiting` or `shell`, is for the chat view, through its adapter, still to come.

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

## The chat view (being built)

Magnus's go, 2026-11-19, for a first version to test. A tab shows its session as a chat of the plugin's own: the
conversation in an editor pane, and an input box that is an editor too, with Claude Code's own UI, the TUI, hidden in
the same tab, a key away. Why, in his words: "Text editing in claude code sucks ... Virtually NONE of the absolutely
standard text editing stuff works", "Virtually all rider/idea shortcuts break in the tui", and "Normal search in the
transcript sucks ... in both the search provided by rider and the search provided by claude code". The TUI stays for
what the chat doesn't do: "I would have access to the full / command stuff etc, but all the every day back and forth
talking would happen in a UI that does not make me want to scream".

### Who provides what: the chat is the plugin's, Claude Code the hub's

**Agreed on 2026-11-19 with the tooling owner, at Magnus's request,** in `~/.claude/dev_docs/the-claude-tooling-bundle.md`:
the Claude tooling is one bundle, and each concept has one home in it. ClaudeTooling's resident server, the hub, owns
everything about Claude Code: its session records, its transcripts read into a conversation, a session's line across
`/clear`, resume, fork and compaction, and typing into its TUI, the shell under it included. The plugin owns the IDE:
the tabs, the terminals, the chat's rendering, its input box, images saved to files, links and windows. The hub serves
the plugin over loopback HTTP and JSON, with Server-Sent Events for what it follows. So the plugin needs the hub, which
Magnus agreed to that day: "Yes to both. How, and even if, we publish this is a question for the future. First we make
it Great(TM)".

So the chat holds no transcript reader and no typing of its own. It talks to one interface,
**`ClaudeCodeBehindTheChat`**: a `StateFlow` of the conversation, whole, a `StateFlow` of Claude Code's status
(`NOT_RUNNING`, `IDLE`, `BUSY`, `WAITING`), and `send(Message)`, `interrupt()` and `changeThePermissionMode()`, each
suspending until Claude Code got it, or with why not, its `TypingOutcome`. A `Message` is its parts as they came into
the box: `TypedText`, `PastedText`, `PastedImage` (the file). A **`ClaudeCodeBehindTheChatProvider`**, on the plugin's
extension point `dev.magnus.claudesessions.claudeCodeBehindTheChat`, gives a tab one. The hub's is the adapter
**`TheHubBehindTheChat`**: the status of the tab's Claude Code from the running sessions, and typing through
`TypingIntoClaudeCode`, each refusal's `reason` read into a `TypingOutcome` (`TheHubBehindTheChat.outcomeOf`). Its
conversation is none until the hub tells of one, so no provider gives it yet, and **only the sandbox IDE's tabs have a
chat**: the stand-in `StandInBehindTheChat`, which the specifications drive through the observation channel. In every
other IDE no provider gives a tab one, and it shows its terminal alone, as before.

### What the first version does, as built

- **Two cards in each tab, in a `CardLayout`:** the chat and the terminal (`ChatAndTerminalCards`), the tab's content
  in place of the terminal's component. A tab gets them as the view opens it or takes it back after a reload, and gives
  them up, its content the terminal again, before it's parked for a reload and as it closes.
  - **What the tab shows by itself:** the chat while Claude Code is busy or idle, and the terminal while it doesn't
    run, and while it's `waiting`: Claude Code's record says so whenever a dialog is open, a permission prompt, a
    question, or a picker a slash command opened (`function B4o` in Claude Code 2.1.328's code), which only the TUI
    draws. When that changes, the tab switches, and back to the chat once Claude Code is busy or idle again, only if it
    was the tab that switched: a card the user chose stays. The terminal's card is shown until Claude Code runs, so a
    tab whose shell starts only once it's shown starts it, at its size.
  - **The switch:** `SwitchBetweenTheChatAndTheTerminalAction`, a button in the tool window's title bar, and
    **Ctrl+Alt+Shift+Z**, free in the default keymap, both Visual Studio keymaps and Magnus's own on 2026-11-19 (a
    specification checks the bundled ones). Its shortcut works in the terminal too, through the terminal's
    `allowedActionsProvider` extension point, as the terminal otherwise sends keys to the program it runs.
- **The transcript pane** (`TranscriptPane`), a read-only editor: the IDE's search, selection and shortcuts work in it.
  Its text is markdown, highlighted as markdown where the IDE has the Markdown file type, found by name, so no
  dependency on the Markdown plugin.
  - **Each prompt under `## You`, each turn of Claude's under `## Claude`;** a slash command as typed; a compaction as
    a divider line.
  - **Each tool call as its header,** such as `▸ Bash: git status` (`ToolCallHeader`: the field that says what it ran
    on, for Claude Code's own tools), with its input and its result folded away below it, collapsed, in a fenced code
    block fenced with more backticks than any run inside.
  - **It shows anew only from the first item that differs** from what it shows: a tool call's result coming, or a
    rewind, redraws from there, so what the user scrolled to, selected or opened above stays. A fold the user opened
    stays open when its tool call is redrawn. It follows the end while scrolled to the end.
  - **Links:** every filter on the platform's `consoleFilterProvider` extension point runs over new text, **each by
    itself**, as the terminal runs them: a filter's result ends the filtering of its line unless it says otherwise
    (`Filter.NextAction.EXIT` by default), so a `CompositeFilter` let the symbol linker's symbol links hide its file
    links on the same line. Links over text taken away go with it.
- **The input box** (`MessageBox`), an editor of a plain-text `LightVirtualFile`. Enter sends, Shift+Enter starts a new
  line, Escape interrupts Claude Code, Shift+Tab changes its permission mode, each a key of the box's own, ahead of the
  keymap's; Enter, Shift+Enter and Escape go to a completion list instead while one shows.
  - **The IDE's paste,** whatever the keymap binds it to, goes through `MessageBoxPasteHandler`, the first handler of
    `EditorPaste`: text is inserted and marked as pasted; an image with no text on the clipboard is saved as PNG by
    `PastedImages`, in the IDE's system folder, kept a week, and shown as `[Image #n]`. Every other editor's paste is the
    one it had.
  - **The message's parts:** each stretch of the text as what it came in as, typed, pasted, or an image, the innermost
    paste deciding; two pastes side by side stay two, as in Claude Code's box.
  - **A message that starts with `/`** shows the terminal, as if the user chose it, where the command shows what it does.
  - **The box is free for the next message at once:** `TypingFromTheChat` gives Claude Code what the user gave it, in
    turn, in the order given, as giving one takes a second or two, and minutes while other typing has the session's
    turn.
  - **What Claude Code didn't get comes back:** the message goes back into the box, ahead of what the user wrote since,
    each part marked as it came in, and the status line says why, in red, until Claude Code gets the next. The messages
    that waited after it come back with it, a line apart, rather than overtake it; the keys that waited are dropped.
    When what is in the way shows in Claude Code's own UI, a dialog or text in its own input box, the tab shows the
    terminal. A message the hub typed and left unsent in Claude Code's own box (`inputBoxShowsOtherThanTyped`,
    `notSentByEnter`) isn't put back: the terminal shows it there.
- **A status line** above the input box: whether Claude is busy, idle or waiting in the terminal, and how many commands
  the turn has run, counted from the conversation.

### What's left for the first version

- **The hub's side, and the plugin's adapter to it,** as agreed, a step at a time in the bundle's order of building:
  the records stream, which the plugin follows since 2026-11-19 (above, "Which session a tab runs"); then typing, the
  shell included, through which the tabs start Claude Code since 2026-11-19, and through which `TheHubBehindTheChat`
  sends, interrupts and changes the permission mode, built and not yet given to a tab; then the conversation of a
  session's whole line, across compactions and `/clear` (Magnus: "One
  session just restarted itself with /clear and the entire history disappeared for me. I would love to still have it
  there"); then the sessions on disk. Then the stand-in stays for the chat's specifications, and new ones run end to end
  through the hub.
- **One imitation of Claude Code, the hub's:** the hub's own specifications run a stand-in for Claude Code
  (`~/.claude/src/StandInForClaudeCode`) that draws and takes an input box as Claude Code does. Asked of the tooling
  owner on 2026-11-19: it grows what the sandbox's fake does (records, transcripts, `--name`, `--resume`, `/clear`,
  `/rename`, `/exit`, the conversation and the terminal's size on its screen), and a busy state and a mode line for the
  chat's specifications, and is published as a built `claude.exe`. The sandbox then runs it, and `tools/FakeClaudeCode`
  goes. Until then the fake has no input box, and nothing types into it through the hub but the shell's line.
- **Not tried with the real Claude Code:** the chat with a hub behind it; the real TUI drawn through hiding and showing
  its card (only the fake's sizes were seen).

## The chat view: what the spikes found

For the chat view Magnus asked about on 2026-11-19: the session's transcript, and an input box, of the plugin's own, with
Claude Code's own UI hidden in the same tab. Two spikes in the sandbox that day, with Rider 2026.3 and Claude Code
2.1.328: throwaway code on the branch `spike/chat-ui`, not merged.

**A hidden terminal, as a card of a `CardLayout`:** the tab's content a panel holding a chat card and the terminal's
component, the chat card shown.

- **It's laid out while hidden:** a `CardLayout` gives every card the panel's bounds, so the hidden terminal knows its
  grid, and follows the tool window's and the IDE window's resizes. The fake Claude Code was told of each, in columns
  and rows, while hidden.
- **With `deferSessionStartUntilUiShown(true)`, its shell doesn't start while its card is hidden,** and starts at the
  right size once the card is shown. So: show the terminal's card until the shell has started, then the chat card.
  Claude Code then started at the tab's size.
- **With `deferSessionStartUntilUiShown(false)`, the shell starts at once, and Claude Code at 80 by 20,** though the
  terminal knew its grid of 113 by 11: the trap in "The terminal API, and its traps". Showing the card doesn't fix it; a
  change of size does. A gap of 20 pixels at the top and bottom of the cards, taken away 300 ms later, sent two resizes,
  113 by 14 and then 113 by 16; taken away at once, it sent none. The same nudge may fix the `bug:` at
  `ClaudeSessionsView.resume`: not tried.
- **Not seen:** the real Claude Code's fullscreen drawing through a hide and a show; only the fake's sizes were.

**Sending into the real Claude Code:**

- **A bracketed paste** (`createSendTextBuilder().useBracketedPasteMode()`) of lines lands in the input box as
  `[Pasted text #1 +3 lines]`, without submitting, and a `\r` after submits it, its newlines intact. But Claude Code
  records it wrapped in `<pasted_content id="…">` tags, and the model is told that such text was pasted from elsewhere
  and may hold instructions the user didn't write. So what Magnus types must not be sent as a paste.
- **Typed instead:** the text sent as it is, with a line feed between lines (Ctrl+J, Claude Code's `chat:newline`), and
  `\r` after, is recorded as plain text, newlines intact. But long text is taken for a paste even without the brackets:
  a single write of 1,500 characters became `[Pasted text]`, and so did writes of 1,000 characters 50 ms apart. Writes
  of 200 characters about 130 ms apart, 6,442 characters in 60 lines in 4.2 seconds, were recorded as plain text. The
  limit lies between 800 and 1,000 characters a write: the tooling owner's check on Claude Code 2.1.328, typing into a
  console with `WriteConsoleInput`, had 500 and 800 recorded as typed and 1,000 as pasted (ClaudeTooling's `InputBox`,
  2026-11-19). The input box shows what the model gets: `[Pasted text #n]` there means a paste.
- **So:** type what he typed, in small pieces, and send what he pasted into the box as a bracketed paste of its own,
  which Claude Code then marks as pasted, as it does a paste into its own box.
- **Images:**
  - A bracketed paste of an image file's path, alone, attaches the image as `[Image #n]`: the transcript's entry has an
    image block and `imagePasteIds`, and the model saw it. A path inside other text, pasted or typed, stays text, and
    the model reads the file with its Read tool.
  - An image on the system clipboard, and Alt+V (Claude Code's `chat:imagePaste` on Windows, Ctrl+V elsewhere), attaches
    it too. That takes the system clipboard, Magnus's: the spike saved his text first, and put it back.
  - So: save a pasted image to a file, and send its path as a bracketed paste of its own. That leaves his clipboard
    alone.
  - **What Magnus needs (2026-11-19):** Ctrl+V of an image on the clipboard, into the chat's input box, puts it in his
    message, as Ctrl+V does in Claude Code's box today: "I cannot remember attaching a file in any other way than from my
    clipboard ever." The file and its path are the plugin's way of handing it on, unseen by him. Not tried: a path
    pasted mid-message, between typed pieces, so that the image lands where he pasted it.
- **Escape twice clears the input box.** Ctrl+L, `chat:clearInput` in Claude Code's keymap, sent as `0x0C`, didn't.
- **The real Claude Code ran with `--setting-sources project,local`,** and `--settings` with only `"tui": "fullscreen"`,
  so that none of Magnus's hooks ran, such as the notification at a turn's end.

## Sessions that outlive the IDE (being built)

Magnus's go, 2026-11-19, on the proposal in `~/.claude/dev_docs/sessions-that-outlive-the-ide.md`. The hub starts a
session host for each tab: a process outside the IDE's job that owns a pseudo console, with the shell in it. The tab's
process becomes an attach client, `ClaudeTooling.SessionHost.exe attach <id>`, that relays between the tab's console
and the host. When the IDE dies, only the attach client dies. Closing a tab ends its session (Magnus: "End it"); the IDE
exiting, crashing, or reloading the plugin only detaches. survive-IDE-restart builds the hub's side.

**The hub's API, as agreed with survive-IDE-restart on 2026-11-19** (its contract will be in `the-hubs-local-api.md`):

- **`POST /session-hosts`** with the shell's command line, its directory, an `environment`, an optional size, and a
  `label` the hub keeps for the plugin. The answer names the host, its shell's process, and the `attachCommand` for the
  tab's `shellCommand`. `GET /session-hosts` lists the hosts of the hub's Claude Code folder; `POST
  /session-hosts/{id}/end` closes the host's pseudo console, as closing a terminal tab does today.
- **Each running session gains `sessionHost`,** which pairs a tab with its Claude Code in place of
  `RunningClaudeCodes.inOrBelow`.
- **`shell`:** the command line of the terminal's shell setting, without Rider's shell-integration arguments.
- **`environment`:** the whole environment Rider's terminal gives a shell, as its `LocalOptionsConfigurer` builds it in
  2026.3: the IDE's own (unless the user unticked passing it), then `TERM=xterm-256color`, `COLORTERM=truecolor`,
  `TERMINAL_EMULATOR=JetBrains-JediTerm`, a new `TERM_SESSION_ID`, then the user's own variables from the terminal's
  settings. The whole of it, because the sandbox IDE's environment holds what its shells need: `CLAUDE_CONFIG_DIR`,
  `CLAUDE_TOOLING_LOCAL_API_FOLDER`, `PATH` with the fake Claude Code first. The hub sets `CLAUDE_CONFIG_DIR` to its own
  folder, or removes it for `~/.claude`'s hub.
- **No size:** a tab's attach client starts only once the tab is first shown, at the tab's real size, and its resize
  repaints Claude Code's screen. The session runs in its host meanwhile, so the 80-by-20 trap goes.
- **The plugin keeps each tab's host with its session,** attaches by it at the IDE's start, and finds by `label` the
  hosts it opened and didn't save. After an attach client exits, `GET /session-hosts` tells whether its host ended.
- **The sandbox** copies the session host's exe with the hub's, and ends its hosts as it stops: they would outlive it.

**What the plugin's spike found, 2026-11-19** (a probe in the hub's throwaway hosts, with Claude Code's own console
mode, input `0x208`):

- **Every input passes two pseudo consoles as it passes one:** typed text, Enter, Escape then Enter, Ctrl+J, CSI-u,
  SGR mouse clicks and wheel, bracketed paste, back-tab, arrows, focus. Rider's terminal sends a plain CR for Shift+Enter
  (JediTerm's `TerminalKeyEncoder` in 2026.3), with a session host or without.
- **An attach loses the modes the program set before it:** alternate screen, mouse tracking, focus reporting and
  bracketed paste reach the terminal only as the program sets them, and the repaint on attach sets none again. Without
  them, Rider would draw the fullscreen renderer into its scrollback, the wheel wouldn't scroll Claude Code, and a
  multi-line paste into the TUI would be sent at its first line. So the host must set them again for each new attach
  client, before the repaint: told to survive-IDE-restart.

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
