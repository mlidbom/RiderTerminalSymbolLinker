# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.2]

### Added
- **GitHub-style `#L` line anchors on file references now link.** A file reference written with a
  `#L<line>` or `#L<start>-<end>` anchor — the form Rider's *add file to chat* injects, e.g.
  `@Vantage\Domain\Desktops\Desktop.cs#L136-138` — now underlines across the whole path **and** anchor,
  and the click navigates to that line (a `#L<start>-<end>` range **selects** those lines in the opened
  editor), matching the existing `:line` / `:start-end` colon suffix. A leading `@` stays outside the
  link, and a bare `#` not followed by `L<digit>` is still a span boundary, so `#Vantage/…` keeps linking
  just its path portion as before.

## [0.2.1]

### Added
- **Qualified `Type.Member` references link as a single symbol.** A dotted access like
  `ApplicationWindow.ForceToFront` now underlines as one link spanning the whole qualified name —
  instead of two adjacent links — whenever that member-of-type pair actually exists in the solution.
  The click resolves the exact member, so it lands precisely rather than offering every method that
  shares the name. A fully-qualified `Foo.Bar.ApplicationWindow.ForceToFront` links its real
  `ApplicationWindow.ForceToFront` tail; a dotted pair that isn't a real member of that type (e.g.
  `Logger.Info` where `Info` belongs to some other type) still links each known segment on its own,
  exactly as before.

## [0.2.0]

### Added
- **Clickable file references** in terminal output, alongside the existing symbol links. A path links when
  its whole path-shaped portion is a real file in the open solution — including bare names (`App.axaml.cs`),
  partial paths (`Shell\AppShell.cs`), absolute paths, either slash direction, and references touching
  surrounding punctuation (`Update(C:\…\AppShell.cs)`) that Rider's native path detection misses. A path
  with a segment that doesn't exist (`nonexisting/Vantage/Shell/AppShell.cs`) does **not** link, even when
  a file of that name exists elsewhere. Unlike Rider's native links, these are visible from the start, not
  only on Ctrl-hover.
- A `:line`, `:start-end` or `:line:col` suffix navigates to that line; a `:start-end` range **selects**
  those lines in the opened editor.
- File disambiguation matches symbols: a partial or absolute path that pins one file opens directly; a
  bare name shared by several files shows a searchable picker (Ctrl-click opens in the background and
  keeps it open).
- In both the symbol and file disambiguation pickers, entries whose file is already open in the IDE get
  a tinted background band and an "(open)" tag.

### Changed
- The terminal menu action is now **Refresh Terminal Links** and rebuilds both the symbol and file indexes.

## [0.1.7]

### Changed
- Improve plugin description

## [0.1.6]

### Changed
- Remove the word "Rider" from plugin.xml so that Jetbrains will allow publishing this.

## [0.1.5]

### Changed
- The plugin now declares a hard dependency on the **MCP Server for Code Intelligence** plugin
  (`com.j-light.resharper-mcp`) — the ReSharper MCP it uses to resolve C# symbols. The Marketplace
  now prompts you to install it alongside this plugin, instead of the plugin installing and silently
  doing nothing without it.

## [0.1.4]

### Fixed
- The plugin is now correctly marked **Rider-only**. It previously declared only platform-level
  dependencies, so the Marketplace would have offered it to every IntelliJ-based IDE (IntelliJ IDEA,
  PyCharm, …) where it can't work — it needs Rider's ReSharper backend. Added a dependency on
  `com.intellij.modules.rider` so it only appears for Rider.

## [0.1.3]

### Changed
- Dropped the PascalCase-only restriction on linkification. Any identifier that names a real solution
  symbol is now clickable — including underscore-prefixed fields (`_cache`), `ALL_CAPS` constants and
  camelCase members. The symbol index is the sole authority on what links; nothing is underlined by
  identifier shape. (A 3-character minimum still applies as a noise guard.)

## [0.1.2]

### Added
- Plugin icon, shown in the IDE plugin list and on the JetBrains Marketplace.

## [0.1.1]

### Fixed
- Large solutions no longer break the index. On restart the startup build could run before ReSharper
  finished loading the solution, get an empty result, and overwrite both the in-memory index and the
  disk cache with nothing. An empty enumeration is now treated as "not ready" (never cached or shown),
  and the startup build retries until ReSharper can answer.

## [0.1.0]

First public release.

### Added
- Clickable C# symbol names in Rider's reworked terminal — a click navigates to the declaration.
- Symbol resolution via the ReSharper MCP: one match jumps straight there, several show a
  searchable picker, none shows a brief notice, MCP down falls back to Search Everywhere.
- Underlining gated to real solution symbols, so prose isn't littered with false links.
- Positional-record properties (e.g. `record struct AnimationStyle(TimeSpan Duration, …)`) resolve
  to the declaring type's source line instead of dead-ending on `[no source]`.
- **Refresh C# Symbol Links** action in the terminal right-click menu: rebuilds the index and
  re-highlights output already on screen.
- Per-solution disk cache: the index is available instantly on solution open, then rebuilt fresh
  in the background.
- Multi-solution support: each call targets the correct open solution.

[0.2.2]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.2.1...HEAD
[0.2.1]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.7...v0.2.0
[0.1.5]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.4...v0.1.5
[0.1.4]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/mlidbom/RiderTerminalSymbolLinker/releases/tag/v0.1.0
