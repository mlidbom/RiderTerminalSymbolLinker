# How the plugin works

The .NET Terminal Symbol Linker (plugin id `dev.magnus.riderterminalsymbollinker`, Kotlin package `dev.magnus.csl`, from
its first name, ClaudeSymbolLinker) makes .NET symbol names and file paths in Rider's terminal output clickable. It was
built so that Magnus can read Claude Code's output in Rider's terminal and jump to what it names, without VS Code beside
Rider. Rider's reworked terminal links plain `file:line` paths itself, on Ctrl-hover; bare symbol names were the gap.

It only reads what the terminal prints. It never touches the Agent SDK, `claude -p` or ACP.

## Linking: console filters gated by indexes

The plugin registers a `com.intellij.consoleFilterProvider` (`SymbolLinkFilterProvider`), the stable extension point
that the reworked terminal honours. It gives two filters:

- **`SymbolLinkFilter`** links a token only if the `SymbolIndex` knows it: the index, not the token's shape, decides.
  A lone token links if it's a known short name; a dotted `Type.Member` links as one symbol if that pair exists
  (`SymbolReferences`). Tokens inside a path or URL are skipped (`PathSpans`), so a path segment isn't relinked.
- **`FileLinkFilter`** links a path whose every segment lines up with the end of a real solution file's path
  (`SolutionFileIndex`), with an optional `:line` or `:start-end`. Its links are underlined from the moment they're
  printed, unlike the terminal's own.

Both link nothing until their index is ready.

## Where the symbols come from

**C# symbols come from the .NET Analysis&Refactoring MCP**, the plugin it depends on: Magnus's fork of Joshua Light's
ReSharper MCP, in `C:\Dev\resharper-mcp`, whose session is extend-mcp. Its server runs inside Rider's ReSharper
backend, on port 23751 or the port `DOTNET_ANALYSIS_AND_REFACTORING_MCP_PORT` sets, and answers plain JSON-RPC over HTTP
(`POST /`, method `tools/call`, arguments under `params.arguments`). `ReSharperMcp` is the client. Until 0.2.8 it was
the upstream ReSharper MCP (`com.j-light.resharper-mcp`, port 23741).

**Not Rider's `GotoSymbolModel2`:** the platform's own symbol search doesn't see C# symbols; it answers empty at once.
That caused the early intermittent failures. **Nor JetBrains' built-in MCP server:** its `search_symbol` is file-level
and fuzzy, and its syntax tree tool covers only Java and Kotlin.

The tools the plugin uses, and their traps:

- **`list_solutions`** maps the Rider project to its solution (`McpSolution`). Every other call passes that solution's
  `uniquePathSegment` as `solutionName`, because two open solutions can share a name, as two worktrees of one repository
  do.
- **`browse_namespace`**, from the root and then batched by namespace, lists every type.
- **`get_symbol_info` with `includeMembers`**, called once per type by its namespace-qualified name, lists the members.
  Its batch form (`symbols: [...]`) with `includeMembers` fails with `Cannot get runtime features with Universal`
  and drops the whole batch, so it is called singly. `list_symbols_in_file` answers empty, which is why members are
  read per type.
- **`go_to_definition`** resolves a click exactly: one location as text, or a JSON list of candidates when ambiguous.
- **`search_symbol`** matches substrings and stops at 50 results, so short common names drown: not for exact
  resolution.
- **C# 14 extension blocks:** ReSharper lists a block as a synthetic `$extension` member and its methods again,
  flattened, beside it; the plugin collapses the pair. Its properties are listed flattened only as their accessors, such
  as `get_Diameter`, so they don't link yet: a bug in the MCP's `get_symbol_info`, which Magnus decided to wait for
  (2026-11-17). See the `bug:` comment in `ReSharperMcp.parseMembers`.

**Enumeration is sequential by design:** the ReSharper backend serializes analysis, so calls from six threads measured
about 1.2 times faster at best.

## Keeping the links current

- **Disk cache:** `SymbolIndexCache` keeps each solution's names in Rider's system folder, under
  `claude-symbol-linker/<project.locationHash>`, so links work at once when a solution opens, while the index is rebuilt
  in the background (`SymbolIndexLoader`).
- **Re-highlighting output printed before the index was ready:** there is no public call for it. The reworked terminal
  recomputes its filters and rescans the visible output whenever the `consoleFilterProvider` extension point changes, so
  `TerminalLinks` registers and at once unregisters a provider that does nothing.
- **Clicks resolve live** through `go_to_definition`, whatever the index holds: one location opens, several show the
  `LinkPicker`, none notifies, and an unreachable server falls back to Search Everywhere.
- **"Refresh Terminal Links"** in the terminal's context menu rebuilds both indexes.

## The plugin.xml traps

- **`<extensions defaultExtensionNs="com.intellij">`,** never `defaultExtensionPointName`: the wrong attribute binds
  every extension to an empty namespace, silently. The plugin loads and shows as enabled, nothing fails, and none of
  its extensions run. The only trace is an info line in idea.log: `extension point '.consoleFilterProvider' which was
  not found`.
- **`<depends>com.intellij.modules.rider</depends>`** keeps the Marketplace from offering it to IDEs it can't work in.
