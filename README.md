# .NET Terminal Symbol Linker

Makes .NET symbol names and full or partial file paths in Rider's terminal output clickable — a
click jumps to the symbol's declaration, or opens the file. Helpful for working with
[Claude Code](https://www.anthropic.com/claude-code) and other agents that run in the terminal.

[![Install from JetBrains Marketplace](https://img.shields.io/badge/JetBrains%20Marketplace-Install-blue?logo=jetbrains)](https://plugins.jetbrains.com/plugin/32281)
[![Version](https://img.shields.io/jetbrains/plugin/v/32281?label=version)](https://plugins.jetbrains.com/plugin/32281)
[![Build](https://github.com/mlidbom/RiderTerminalSymbolLinker/actions/workflows/build.yml/badge.svg)](https://github.com/mlidbom/RiderTerminalSymbolLinker/actions/workflows/build.yml)

## Screenshots

<p align="center">
  <a href="screenshots/Click-link.png"><img src="screenshots/Click-link.png" width="48%" alt="Click a symbol name in terminal output to jump to its declaration"></a>
  <a href="screenshots/Disambiguate-link.png"><img src="screenshots/Disambiguate-link.png" width="48%" alt="Several matches open a searchable picker to disambiguate"></a>
</p>

<p align="center"><em>Click a symbol to navigate (left); several declarations open a searchable picker (right). Click an image for full size.</em></p>

## Features

- **Click a symbol to navigate to the correct file and line.** If there is more than one match, you get a list to chose from.
- **Qualified names link as one symbol.** A dotted `Type.Member` reference like
  `ApplicationWindow.ForceToFront` links as a single span — not two adjacent tokens — when that
  member-of-type pair actually exists, and the click resolves the exact member instead of every
  method that shares the name.
- **Click a file reference to open it.** Bare names (`App.axaml.cs`), partial paths
  (`Shell/AppShell.cs`), absolute paths, either slash direction, even references touching punctuation
  (`Update(C:\…\AppShell.cs)`) — anything whose whole path resolves to a real solution file. A `:line`
  or `:start-end` suffix navigates to that line or **selects** that range; several files of one name
  open the same searchable picker. These show as links from the start, where Rider's own path
  detection reveals them only on Ctrl-hover and misses most of the above.
- **Refresh Terminal Links** in the terminal's right-click menu — rebuilds the symbol *and* file
  indexes *and* re-highlights output already on screen, so a symbol or file you just created lights
  up retroactively.
- **Instant symbols on open.** A per-solution disk cache makes symbol links work immediately on
  solution open; a fresh index is rebuilt in the background.

## Requirements

- **Rider 2026.1+** (built and tested against 2026.2; the reworked terminal must be the active
  terminal engine).
- The **ReSharper MCP** plugin — [`joshua-light/resharper-mcp`](https://github.com/joshua-light/resharper-mcp),
  listed in Rider as `com.j-light.resharper-mcp` — installed and running. This is what resolves and
  enumerates C# **symbols** (Rider's own `GotoSymbolModel` does not see them reliably), and the plugin
  declares a hard dependency on it. **File** links don't use the MCP, so they keep working even when
  the MCP server isn't running — only symbol links go quiet.

## Install

### From the JetBrains Marketplace

[![Install from JetBrains Marketplace](https://img.shields.io/badge/JetBrains%20Marketplace-Install-blue?logo=jetbrains)](https://plugins.jetbrains.com/plugin/32281)

In Rider: **Settings → Plugins → Marketplace**, search for **.NET Terminal Symbol Linker**, and click
**Install** — or open the [Marketplace page](https://plugins.jetbrains.com/plugin/32281) directly. Rider
prompts you to also install the required ReSharper MCP plugin alongside it. **Restart** Rider when done.

### From a downloaded zip

For a pre-release build, or to install without the Marketplace:

1. Download the latest `RiderTerminalSymbolLinker-<version>.zip` from
   [Releases](https://github.com/mlidbom/RiderTerminalSymbolLinker/releases).
2. Rider → **Settings → Plugins → ⚙ → Install Plugin from Disk…** → pick the zip.
3. **Restart** Rider.

## Build from source

The build needs a **JDK 21** for its Gradle toolchain. If your machine only has a newer JDK/JBR (a
Rider install ships JBR 25, which the toolchain won't use), keep a JDK 21 under `.tooling\jdk` (a
gitignored, machine-local folder) and build through the wrapper, which points `JAVA_HOME` at it **for
that one process only** — nothing global is changed:

```powershell
# Builds the plugin (buildPlugin); zip lands in build\distributions\:
.\build.ps1

# Any arguments forward straight to Gradle:
.\build.ps1 clean buildPlugin --console=plain
```

If you already have a JDK 21 on `JAVA_HOME` (or on the Gradle launcher), you can skip the wrapper and
call Gradle directly:

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
