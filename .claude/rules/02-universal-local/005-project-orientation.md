# Project orientation

The .NET Terminal Symbol Linker: a Rider plugin, in Kotlin under `src/main/kotlin/dev/magnus/csl`, that makes .NET
symbol names and file paths in Rider's terminal output clickable. It gets C# symbols from the .NET Analysis&Refactoring
MCP, the plugin it depends on (`C:\Dev\resharper-mcp`, whose session is extend-mcp).

- **`dev_docs/architecture.md`:** how the plugin links and resolves, where the symbols come from, and the MCP tools'
  and plugin.xml's traps. Read it before changing the plugin's code.
- **`dev_docs/development-environment.md`:** building, the sandbox Rider, the observation channel, the specifications,
  and deploying to Magnus's Rider.
- **`dev_docs/releasing.md`:** versions, which Riders it supports, CI and the Marketplace.
- **The rest:** `src/test` holds the Kotlin unit tests, `tools/` the C# development tools, and `tests/` the fixture
  solution and the sandbox specifications.
