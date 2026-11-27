# How the plugin works

The .NET Terminal Symbol Linker (plugin id `dev.magnus.riderterminalsymbollinker`, Kotlin package `dev.magnus.csl`, from
its first name, ClaudeSymbolLinker) makes .NET symbol names and file paths in Rider's terminal output clickable. It was
built so that Magnus can read Claude Code's output in Rider's terminal and jump to what it names, without VS Code beside
Rider. Rider's reworked terminal links plain `file:line` paths itself, on Ctrl-hover; bare symbol names were the gap.

It only reads what the terminal prints. It never touches the Agent SDK, `claude -p` or ACP.

## Linking: console filters gated by indexes

The plugin registers a `com.intellij.consoleFilterProvider` (`SymbolLinkFilterProvider`), the stable extension point
that the reworked terminal honours. It gives two filters:

- **`SymbolLinkFilter`** links a name only if the `SymbolIndex` knows it: the index, not the name's shape, decides.
  It holds the qualified names of the solution's types and members, and of the namespaces their types are in and
  around them (`DeclaredNames`). Words joined by dots link as one symbol as far as they are the end of a qualified
  name, from each word the most words that are; two words or more that end a namespace's name link as the namespace;
  a word alone links from two letters (`SymbolReferences`). Names inside a path or URL are skipped (`PathSpans`), so
  a path segment isn't relinked. Cloyd's chat links names the same way, and its specifications list the same cases.
- **`FileLinkFilter`** links a path whose every segment lines up with the end of a real solution file's path
  (`SolutionFileIndex`), with an optional `:line` or `:start-end`. Its links are underlined from the moment they're
  printed, unlike the terminal's own.

Both link nothing until their index is ready.

## Where the symbols come from

**C# symbols come from the .NET Analysis&Refactoring MCP**, the plugin it depends on: Magnus's fork of Joshua Light's
ReSharper MCP, in `C:\Dev\resharper-mcp`, whose session is extend-mcp. Its server runs inside Rider's ReSharper
backend, on port 23751 or the port `DOTNET_ANALYSIS_AND_REFACTORING_MCP_PORT` sets, and answers plain JSON-RPC over HTTP
(`POST /`, method `tools/call`, arguments under `params.arguments`). `DotNetAnalysisAndRefactoringMcp` is the client. Until 0.2.8 it was
the upstream ReSharper MCP (`com.j-light.resharper-mcp`, port 23741).

**Not Rider's `GotoSymbolModel2`:** the platform's own symbol search doesn't see C# symbols; it answers empty at once.
That caused the early intermittent failures. **Nor JetBrains' built-in MCP server:** its `search_symbol` is file-level
and fuzzy, and its syntax tree tool covers only Java and Kotlin.

**The plugin reads the tools' JSON form alone,** which the argument `"format": "json"` asks for, since 0.2.11 and the
MCP's 0.15.0: a contract, where the text form is written for people and changes freely. `McpJsonContract.kt` mirrors
what the plugin reads of it; the MCP's specifications of the form are in its repository, under
`tests/DotNetAnalysisAndRefactoringMcp.Specifications/JsonForm`, and its CLAUDE.md lists what the plugin relies on, so
that extend-mcp tells this project before changing it. An older MCP ignores the argument and answers text, which the
plugin takes as no answer, and logs. A solution still loading answers an error.

The tools the plugin uses:

- **Every call names the solution** by the absolute path of the solution file Rider has open, as `solutionName`
  (`McpSolution`). The MCP matches it by its full path, so the call reaches this very solution in this Rider's backend,
  whatever other solutions share its name or its folder, as two worktrees of one repository do, or Compze's twenty. The
  plugin calls only once the MCP's `GET /health` lists the path: asked before, seconds after the solution opens, the MCP
  would open it again in a Rider of its own. A project with no solution file, such as Rider's home project, makes no
  calls. Until 2026-11-27 the plugin mapped its project through `list_solutions`, by the solution's folder, which
  picked a wrong one in a folder of several.
- **`browse_namespace`** finds every namespace, 50 to a call, and then lists each namespace's types with their
  members, one namespace to a call, nested types included. `DeclaredNamesGatherer` turns them into the index. A C# 14
  extension block is a member of its own kind, whose members count as the declaring class's. A click on a namespace
  lists it and the namespaces under it again, live, without members, for its types' files (`TypesOfNamespaces`).
- **`go_to_definition`** resolves a click exactly: every declaration of the name, with its file, line and column. One
  opens, several show the picker. A member with no source of its own, such as a positional record's property, is where
  it's written. A compiled symbol has no file, and the plugin leaves it out. A type declared in parts is at the part in
  the file named after it, where there is one, since the MCP's change of 2026-11-24. Before that change it was at the
  part ReSharper lists first, which is the part edited last.
- **A declaration a source generator wrote** has no file, since the MCP's change of 2026-11-26, but its `generator` and
  `hintName`, with its line and column in the generated file, which Rider holds in memory (`GeneratedFile`). Rider's
  project model lists that file under its generator, and the plugin opens it as Solution Explorer does
  (`navigateToFileViaBackend`): the backend writes it under `%TEMP%\SourceGeneratedDocuments` and opens it at its
  start, and the plugin puts the caret at the declaration once the editor has opened. In the first seconds after the
  solution opens, the project model lists the file without where it is, until the backend has run the generator: a
  click then says so. Until 0.2.13 the plugin left such declarations out.
- **`search_symbol`** matches substrings and stops at 50 results, so short common names drown: not for exact
  resolution.

**Enumeration is sequential by design:** the ReSharper backend serializes analysis, so calls from six threads measured
about 1.2 times faster at best. One namespace to a call keeps each call short, so other clients' calls don't wait long
behind the plugin's.

## Keeping the links current

- **Disk cache:** `SymbolIndexCache` keeps each solution's names in Rider's system folder, under
  `claude-symbol-linker/<project.locationHash>`, so links work at once when a solution opens, while the index is rebuilt
  in the background (`SymbolIndexLoader`).
- **Re-highlighting output printed before the index was ready:** there is no public call for it. The reworked terminal
  recomputes its filters and rescans the visible output whenever the `consoleFilterProvider` extension point changes, so
  `TerminalLinks` registers and at once unregisters a provider that does nothing.
- **Clicks resolve live** through `go_to_definition`, whatever the index holds: one location opens, several show the
  `LinkPicker`, none notifies, and an unreachable server falls back to Search Everywhere. A namespace's click always
  shows the picker. The links are `HyperlinkInfoBase`s, which the terminal tells where they were clicked, so the
  picker opens there.
- **"Refresh Terminal Links"** in the terminal's context menu rebuilds both indexes.

## The plugin.xml traps

- **`<extensions defaultExtensionNs="com.intellij">`,** never `defaultExtensionPointName`: the wrong attribute binds
  every extension to an empty namespace, silently. The plugin loads and shows as enabled, nothing fails, and none of
  its extensions run. The only trace is an info line in idea.log: `extension point '.consoleFilterProvider' which was
  not found`.
- **`<depends>com.intellij.modules.rider</depends>`** keeps the Marketplace from offering it to IDEs it can't work in.
