# Changelog

All notable changes to Claude Sessions are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0]

### Added
- **The Claude Sessions tool window.** Each of its tabs runs a Claude Code session in your terminal shell: the **+** in
  its title bar starts a new one in the project's folder. When Claude Code exits, the tab stays at the shell's prompt,
  and keeps the session: `claude --resume <id>` typed there continues it in the same tab, as tooling that reloads
  sessions does. The tab closes when the shell exits. The tool window is shown again when the IDE starts, if it was
  shown as the IDE closed.
- **Sessions survive a restart.** When the project opens again, every session that ran in the tool window when it
  closed is resumed in its own tab, in its own folder, in the same order. That includes a session switched to with
  `/resume` or started with `/clear` in a tab, and one whose Claude Code had exited in a tab left open. A session
  archived or deleted since, whose transcript Claude Code no longer has, isn't.
- **Sessions run on through an update of the plugin.** While the plugin reloads, its tabs that run Claude Code wait in
  the Terminal tool window, and come back when it has. Should the plugin be uninstalled, they run on there. A tab whose
  Claude Code had exited comes back running its session, as after a restart.
