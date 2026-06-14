# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.2...HEAD
[0.1.2]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/mlidbom/RiderTerminalSymbolLinker/releases/tag/v0.1.0
