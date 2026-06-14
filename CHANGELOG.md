# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.6]

### Changed
- Improve plugin description

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

[Unreleased]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.5...HEAD
[0.1.5]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.4...v0.1.5
[0.1.4]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/mlidbom/RiderTerminalSymbolLinker/releases/tag/v0.1.0
