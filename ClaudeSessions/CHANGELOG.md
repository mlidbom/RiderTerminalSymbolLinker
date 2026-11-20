# Changelog

All notable changes to Claude Sessions are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0]

### Added
- **The Claude Sessions tool window.** Each of its tabs runs a Claude Code session in your terminal shell: the **+** in
  its title bar asks for the new session's name, and Enter starts it in the project's folder, with that name, and its
  tab named after it. Left empty, the session starts without a name; Escape starts none. The name may have any
  characters, in PowerShell, Windows PowerShell, cmd or a POSIX shell such as bash, but cmd puts a variable's value in
  place of a `%NAME%` in it. When Claude Code exits, the tab stays at the shell's prompt,
  and keeps the session: `claude --resume <id>` typed there continues it in the same tab, as tooling that reloads
  sessions does. The tab closes when the shell exits. A terminal tab that comes into the tool window some other way is
  kept like the others. The tool window is shown again when the IDE starts, if it was
  shown as the IDE closed, with the tab that was selected selected again.
- **Sessions run on when the IDE exits, crashes or restarts.** Each tab's shell, and Claude Code in it, runs in a
  session host of the Claude tooling's hub, outside the IDE, and the tab shows it. When the IDE goes, the sessions run
  on, mid-turn included, and when the project opens again every tab comes back as it was, in the same order, showing
  its session as it is now. Closing a tab ends its session.
- **Each tab shows its session as a chat.** In place of Claude Code's own UI, a tab shows the conversation in an
  editor, where the IDE's search, selection and shortcuts work, and an input box that is an editor too. Each prompt shows
  under `## You`, each turn of Claude's under `## Claude`, and a prompt from another session, a background task or a
  scheduled wake-up under where it came from. Each tool call is one line, its input and result folded below it, and
  thinking is folded too; file paths and symbols are links, as in the terminal. A `/clear` shows as a divider, with the
  conversation going on below it. Ctrl+Enter sends, Enter starts a new line, as in any editor, and so does Shift+Enter,
  as in Claude Code's own box; Escape interrupts Claude, Shift+Tab changes
  its permission mode, and the IDE's paste takes text and images. The box is free for the next message at once; a
  message Claude Code doesn't get comes back into the box, and the status line says why. Claude Code's own UI is under
  the tab's Terminal tab, beside its Chat tab, and Ctrl+H switches between them in a Claude Sessions tab, where it
  takes the place of Replace; the tab shows the terminal by itself while Claude Code waits in a dialog, and for a slash
  command. Texts with Windows line endings, such as a tool's output, show as any other.
- **Nothing written in a chat is lost when the IDE goes.** What is in a chat's input box is saved half a second after
  each change, and as the IDE closes, and comes back as it was after a restart, a crash or a plugin update, the caret
  included. Messages that waited to be given to Claude Code come back into the box, marked as not sent, and are never
  sent again by themselves. The conversation comes back scrolled to where it was, with the tool calls you opened open,
  and a tab shows the chat or the terminal as you chose.
- **Each solution's window goes back to its virtual desktop after a restart.** When the IDE starts again, each
  project's window is moved to the Windows virtual desktop it was on as the IDE closed, rather than opening on the one
  shown then. A desktop removed since is skipped.
- **Other tools can restart the IDE.** Claude Sessions' local API lists the IDE's project windows, with their virtual
  desktops, and restarts the IDE when asked, as after a plugin update: for the
  Claude tooling's tool that restarts Rider. Asking to restart takes a token from the API's announcement.
- **Sessions survive a reboot.** When a tab's session host is gone too, as after a reboot, its session is resumed in a
  new tab, in its own folder, in the same order. That includes a session switched to with `/resume` or started with
  `/clear` in a tab, and one whose Claude Code had exited in a tab left open. A session archived or deleted since,
  whose transcript Claude Code no longer has, isn't.
- **Resume any earlier session from the session history picker.** The history button beside the **+**, or
  Ctrl+Alt+Shift+Y, lists the sessions Claude Code has kept, most recently active first: each by its name, or the title
  Claude Code gave it, with its last prompt and when it was last active. Typing filters them, word by word. It lists
  the sessions of the project's worktree at first; all worktrees of its git repository, or all projects, are a click
  away. Enter or a double click resumes the session in a new tab, in the project's folder, wherever it ran before. A
  session whose Claude Code exited in a tab is resumed in that tab. A session that runs is never resumed again: one
  running in a tab, in this IDE or another, is marked running in green, and choosing it brings its tab forward; one
  running elsewhere, as in a plain terminal, is greyed out, and can't be chosen. Sessions `claude -p` ran are left out.
- **Other tools on the machine can show a session's tab.** Each IDE announces a local API in
  `%LOCALAPPDATA%\ClaudeSessions\ides\<its process id>.json`. A tool whose notification about a Claude Code session
  you click can ask it to show the session's tab: it selects the tab, and shows the tool window.
- **Sessions run on through an update of the plugin.** While the plugin reloads, the sessions run on in their session
  hosts, and the tabs come back as they were when it has, with the same tab selected, and the tool window shown as it
  was. That includes a tab whose Claude Code has exited. A terminal tab that came into the tool window some other way
  runs its shell itself, and its session is resumed in a new tab's session host.

### Changed
- **Which session each tab runs comes from the Claude tooling's hub.** Claude Sessions follows the hub, the Claude
  tooling's resident server, which each Claude Code session starts, for the Claude Codes that run and the session each
  runs, instead of reading Claude Code's own records of them. When a new build of the hub replaces the old one, Claude
  Sessions follows the new one within a second. Should the hub speak a protocol this build of Claude Sessions doesn't
  know, a notification asks you to restart the IDE.
- **The hub starts Claude Code in a tab's shell.** The `claude` line a tab runs, with the session's name or the session
  to resume, is typed into the shell by the hub, quoted for that shell as before. When no hub runs, as when the IDE
  resumes its sessions after a reboot, Claude Sessions starts it. Should the hub not type the line, the tab stays at the
  shell's prompt, and a notification says why.

### Fixed
- **A tab shows Claude Code at the tab's size.** A tab opened by the **+**, or by the session history picker, often
  showed Claude Code drawn 80 columns wide and 20 rows tall until the tool window was resized, and so did the tab
  selected as the IDE started.
