# Changelog

All notable changes to Claude Sessions are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0]

### Added
- **The Claude Sessions tool window.** Each of its tabs runs a Claude Code session: the **+** in its title bar starts a
  new one in the project's folder. A tab closes when Claude Code in it exits.
- **Sessions survive a restart.** When the project opens again, every session that ran in the tool window when it
  closed is resumed in its own tab, in its own folder, in the same order. That includes a session switched to with
  `/resume` or started with `/clear` in a tab.
