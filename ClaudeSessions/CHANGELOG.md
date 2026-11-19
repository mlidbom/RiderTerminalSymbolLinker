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
- **Sessions survive a restart.** When the project opens again, every session that ran in the tool window when it
  closed is resumed in its own tab, in its own folder, in the same order. That includes a session switched to with
  `/resume` or started with `/clear` in a tab, and one whose Claude Code had exited in a tab left open. A session
  archived or deleted since, whose transcript Claude Code no longer has, isn't.
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
- **Sessions run on through an update of the plugin.** While the plugin reloads, its tabs wait in
  the Terminal tool window, and come back as they were when it has, with the same tab selected, and the tool window
  shown as it was. That includes a tab whose Claude Code has exited. Should the plugin be uninstalled, they run on
  there.

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
- **A new session's tab shows Claude Code at the tab's size.** A tab opened by the **+**, or by the session history
  picker, often showed Claude Code drawn 80 columns wide and 20 rows tall until the tool window was resized. A session
  resumed as the IDE starts, in the tab selected then, may still be drawn so.
