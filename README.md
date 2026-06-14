# Rider Terminal Symbol Linker

A JetBrains **Rider** plugin that makes C# **symbol** names in the terminal clickable — click a type
or member name in CLI output and Rider jumps to its declaration. Built for following along with
[Claude Code](https://www.anthropic.com/claude-code) and other CLI tools without keeping a second
editor open just to read their output.

Plain `file:line` links already work in Rider's terminal; bare *symbol names* were the gap. This
fills it, and only linkifies names that are **real solution symbols**, so prose isn't littered with
false links.

[![Build](https://github.com/mlidbom/RiderTerminalSymbolLinker/actions/workflows/build.yml/badge.svg)](https://github.com/mlidbom/RiderTerminalSymbolLinker/actions/workflows/build.yml)

## Features

- **Click to navigate.** One declaration → jump straight there. Several → a searchable picker
  (type to filter, like *Go to Symbol*). None → a brief notice. MCP unreachable → falls back to
  Search Everywhere, so a click is never a dead end.
- **Refresh C# Symbol Links** in the terminal's right-click menu — rebuilds the symbol index *and*
  re-highlights output already on screen, so a class you just created lights up retroactively.
- **Instant on open.** A per-solution disk cache makes links work immediately on solution open; a
  fresh index is rebuilt in the background.
- **Multi-solution aware.** Every lookup targets the correct open solution.

## Requirements

- **Rider 2026.1+** (built and tested against 2026.2; the reworked terminal must be the active
  terminal engine).
- The **ReSharper MCP** plugin — [`joshua-light/resharper-mcp`](https://github.com/joshua-light/resharper-mcp),
  listed in Rider as `com.j-light.resharper-mcp` — installed and running. This is what resolves and
  enumerates C# symbols (Rider's own `GotoSymbolModel` does not see them reliably). Without it the
  plugin loads but does nothing.

## Install

1. Download the latest `RiderTerminalSymbolLinker-<version>.zip` from
   [Releases](https://github.com/mlidbom/RiderTerminalSymbolLinker/releases).
2. Rider → **Settings → Plugins → ⚙ → Install Plugin from Disk…** → pick the zip.
3. **Restart** Rider (the `consoleFilterProvider` extension is non-dynamic).

## How it works

- A `consoleFilterProvider` underlines PascalCase identifiers in terminal output, but only those a
  `SymbolIndex` knows are real symbols.
- The index is enumerated once from the ReSharper MCP (namespaces → types → members), cached to disk
  per solution, and rebuilt in the background on open or on demand.
- On click, the symbol is resolved live via the MCP's `go_to_definition`, so navigation is always
  fresh even when the underline index is a moment stale.
- Re-highlighting existing terminal output is done by nudging the `consoleFilterProvider` extension
  point, which makes the reworked terminal re-run its filters over the visible buffer.

## Build from source

The build needs a **JDK 21** (point `JAVA_HOME` at one, or use the Gradle launcher's JDK).

```bash
# Build against an installed Rider (fast — no platform download):
./gradlew buildPlugin -PlocalIdePath="C:/Users/you/AppData/Local/Programs/Rider"

# Or let it download the pinned stable Rider (what CI does):
./gradlew buildPlugin
```

The plugin zip lands in `build/distributions/`.

Tip: put your local IDE path in your **user** Gradle properties so you don't pass it every time —
add this line to `~/.gradle/gradle.properties`:

```
localIdePath=C:/Users/you/AppData/Local/Programs/Rider
```

## Releases & CI

- **`build.yml`** builds the plugin on every push and PR to `main` (downloading the pinned stable
  Rider) and uploads the zip as a build artifact.
- **`release.yml`** runs when you push a tag `v*`. It builds with the version from the tag and
  creates a GitHub Release with the zip attached:

  ```bash
  git tag v0.1.0 && git push origin v0.1.0
  ```

### Optional: JetBrains Marketplace

Publishing to the Marketplace is wired but disabled by default (it needs no secrets to build or to
release on GitHub). To enable it:

1. Add repo secret `PUBLISH_TOKEN` (a Marketplace permanent token) and, to sign the plugin,
   `CERTIFICATE_CHAIN`, `PRIVATE_KEY`, `PRIVATE_KEY_PASSWORD`
   (see the [signing guide](https://plugins.jetbrains.com/docs/intellij/plugin-signing.html)).
2. Uncomment the *Publish to JetBrains Marketplace* step in
   [`.github/workflows/release.yml`](.github/workflows/release.yml).

The Gradle config already reads those values from the environment.

## License

[MIT](LICENSE) © Magnus Lidbom
