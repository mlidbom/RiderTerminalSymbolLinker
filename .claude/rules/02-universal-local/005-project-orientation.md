# Project orientation

Two JetBrains plugins, in Kotlin, each a Gradle project of its own:

- **The .NET Terminal Symbol Linker,** the root project, under `src/main/kotlin/dev/magnus/csl`: a Rider plugin that
  makes .NET symbol names and file paths in Rider's terminal output clickable. It gets C# symbols from the .NET
  Analysis&Refactoring MCP, the plugin it depends on (`C:\Dev\resharper-mcp`, whose session is extend-mcp).
- **Claude Sessions,** `ClaudeSessions/`, under `src/main/kotlin/dev/magnus/claudesessions`: in any JetBrains IDE, has
  Cloyd show each project's Claude Code sessions as the project opens, and has the IDE's local API, through which the
  Claude tooling restarts it. Its tool window, chat and history picker went with the switch to Cloyd, 0.2.0. A project
  of its own so that it can move to a repository of its own: keep what's its own inside its folder.

IntelliJ IDEA holds the plugins' Gradle projects, for `idea`.

**Cloyd,** the .NET application in Avalonia whose window beside each IDE shows the project's sessions, and which Claude
Sessions runs, was developed here until 2026-11-23. It is the Claude tooling's now, in `~/.claude`: its code in
`src/Cloyd`, its documents in `~/.claude/dev_docs/cloyd/`, its history carried over, and its sessions moved there.
`~/.claude`'s `src/publish.ps1` installs it into `%LOCALAPPDATA%\Cloyd\bin`, which Claude Sessions runs, and which the
sandbox Rider runs a copy of. A change to what Claude Sessions asks of Cloyd is made in both, together.

The documents:

- **`dev_docs/architecture.md`:** how the symbol linker links and resolves, where the symbols come from, and the MCP
  tools' and plugin.xml's traps. Read it before changing the symbol linker's code.
- **`dev_docs/claude-sessions.md`:** how Claude Sessions has Cloyd show the projects, and its local API; and what its
  tool window did until the switch, for the port to Cloyd. Read it before changing Claude Sessions' code.
- **`~/.claude/dev_docs/cloyd/cloyd-as-a-dotnet-application.md`:** Magnus's direction since 2026-11-20, Cloyd as a .NET
  application beside the IDEs with the plugin shrunk to what needs the IDE, and Cloyd as it's built. Read it before new
  work on Claude Sessions.
- **`dev_docs/development-environment.md`:** building, the sandbox Rider, the observation channels, the specifications,
  deploying to Magnus's IDEs, and the Codex helpers in checkouts of their own.
- **`dev_docs/releasing.md`:** the symbol linker's versions, which Riders it supports, CI and the Marketplace.
- **`dev_docs/backlog.md`:** work Magnus deferred, and what waits for a time or an event to be passed on to another
  repository, such as a recommendation for Compze: look at it when what an entry waits for comes about.
- **The rest:** `src/test` holds the symbol linker's Kotlin unit tests, `tools/` the C# development tools, and `tests/` the fixture solution and both plugins' sandbox specifications.
