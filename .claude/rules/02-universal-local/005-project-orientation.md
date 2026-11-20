# Project orientation

Two JetBrains plugins, in Kotlin, each a Gradle project of its own:

- **The .NET Terminal Symbol Linker,** the root project, under `src/main/kotlin/dev/magnus/csl`: a Rider plugin that
  makes .NET symbol names and file paths in Rider's terminal output clickable. It gets C# symbols from the .NET
  Analysis&Refactoring MCP, the plugin it depends on (`C:\Dev\resharper-mcp`, whose session is extend-mcp).
- **Claude Sessions,** `ClaudeSessions/`, under `src/main/kotlin/dev/magnus/claudesessions`: in any JetBrains IDE, has
  Cloyd show each project's Claude Code sessions as the project opens, and has the IDE's local API, through which the
  Claude tooling restarts it. Its tool window, chat and history picker went with the switch to Cloyd, 0.2.0. A project
  of its own so that it can move to a repository of its own: keep what's its own inside its folder.

And **Cloyd,** `Cloyd/Cloyd.slnx`: a .NET application in Avalonia, a window per project beside its IDE, which shows the
sessions: their terminals, for now. Rider holds its solution, for the `jetbrains-rider` and
`dotnet-analysis-and-refactoring` MCPs; IntelliJ IDEA holds the plugins', for `idea`.

The documents:

- **`dev_docs/architecture.md`:** how the symbol linker links and resolves, where the symbols come from, and the MCP
  tools' and plugin.xml's traps. Read it before changing the symbol linker's code.
- **`dev_docs/claude-sessions.md`:** how Claude Sessions has Cloyd show the projects, and its local API; and what its
  tool window did until the switch, for the port to Cloyd. Read it before changing Claude Sessions' code.
- **`dev_docs/cloyd-as-a-dotnet-application.md`:** Magnus's direction since 2026-11-20, its spike's findings, and step one as it's built: Cloyd as a
  .NET application in Avalonia, beside the IDEs, with the plugin shrunk to what needs the IDE. Read it before new work on
  either plugin.
- **`dev_docs/development-environment.md`:** building, the sandbox Rider, the observation channels, the specifications,
  and deploying to Magnus's IDEs.
- **`dev_docs/releasing.md`:** the symbol linker's versions, which Riders it supports, CI and the Marketplace.
- **`dev_docs/backlog.md`:** work Magnus deferred, and what waits for a time or an event to be passed on to another
  repository, such as a recommendation for Compze: look at it when what an entry waits for comes about.
- **The rest:** `src/test` holds the symbol linker's Kotlin unit tests, `tools/` the C# development tools, and `tests/` the fixture solution and both plugins' sandbox specifications.
