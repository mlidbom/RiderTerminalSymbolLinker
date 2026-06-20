# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.6]

### Fixed
- **Identical solution name collision.** When one had two solutions with identical names open, the file from the 
wrong solution would sometimes open when clicking a link.

## [0.2.5]

### Fixed
- **Symbols joined by slashes now link.** A run of symbol names written with separators between them — e.g.
  `IsSupported/CanName/CanSetPerDesktopWallpaper`, as often appears in notes, doc comments and commit
  messages — was mistaken in its entirety for a file path and skipped, so none of the names became links.
  The path-detection that guards against overwriting real file links now requires an actual path signal (a
  file extension, an absolute/relative path anchor like a drive letter or leading slash, or a URL scheme); a
  bare list of identifiers separated by `/` or `\` is no longer treated as a path, so each known symbol in it
  links again. Genuine paths (`src/main/Program.cs`, `C:\Dev\Vantage`, `https://…`) are still left to Rider's
  native file links exactly as before.

## [0.2.4]

### Added
- **A visible first-run notice while the symbol index builds.** The first time you open a solution with no
  cached symbols, terminal links can't work until the symbol index finishes building — which, for a large
  solution, can take a few minutes of waiting on ReSharper. Previously the only sign of this was an easily
  missed status-bar progress bar with generic text, so the terminal just looked like the plugin did nothing.
  Now a sticky notification appears up front explaining that the .NET symbol index is loading and that symbol
  names won't be clickable until it finishes. It clears itself the moment the build completes — replaced by a
  brief "terminal links are active — N symbols indexed" confirmation, or a warning if the index couldn't be
  loaded. Opening a solution whose symbols are already cached stays silent, since links work immediately.

### Changed
- **The background symbol-loading progress now names the plugin.** The status-bar task and its text identify
  it as loading .NET symbols for terminal links — including while it waits for ReSharper to finish loading a
  large solution — instead of generic wording that gave no hint which plugin it belonged to.

## [0.2.3]

### Changed
- **Renamed to ".NET Terminal Symbol Linker."** for clarity and to avoid confusion with potential future forks for JVM, python etc.

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

[0.2.6]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.2.5...HEAD
[0.2.5]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.2.4...v0.2.5
[0.2.4]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.2.3...v0.2.4
[0.2.3]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.2.2...v0.2.3
[0.2.2]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.7...v0.2.0
[0.1.7]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.6...v0.1.7
[0.1.6]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.5...v0.1.6
[0.1.5]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.4...v0.1.5
[0.1.4]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/mlidbom/RiderTerminalSymbolLinker/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/mlidbom/RiderTerminalSymbolLinker/releases/tag/v0.1.0
